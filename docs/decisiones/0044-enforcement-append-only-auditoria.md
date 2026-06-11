# ADR 0044 — Enforcement del invariante append-only en la tabla de auditoría

- **Estado:** Aceptado
- **Fecha:** 2026-06-11
- **Sprint:** 4 — item 6
- **Autor:** Saul Gutierrez

---

## Contexto

El ADR 0041 declara el invariante: **los registros de `auditoria.auditoria`
son inmutables una vez insertados**. Ningún actor (usuario de aplicación,
proceso de mantenimiento, script ad-hoc) debe poder modificarlos ni borrarlos
durante la vida operativa normal del sistema.

El diagnóstico realizado el 2026-06-11 confirma que ese invariante tiene
**cero enforcement**:

1. **En el interceptor (`AuditoriaInterceptor.cs`)**: el guard
   `if (entry.Entity is AuditoriaEntidad) continue` solo evita generar un
   *nuevo* registro de auditoría sobre la propia tabla. No impide que
   `SaveChangesAsync` propague un `Modified` o `Deleted` sobre una
   `AuditoriaEntidad` que haya llegado al `ChangeTracker` por cualquier
   otra vía (consulta + mutación directa desde un handler que tenga el
   `DbContext` inyectado).

2. **En la base de datos**: no existe ningún trigger, ni REVOKE, ni política
   de Row Security en la tabla `auditoria.auditoria`. Un `UPDATE` o `DELETE`
   directo con el usuario `sg_admin` se ejecuta sin obstáculo.

3. **REVOKE descartado antes de escribir**: `sg_admin` es el `POSTGRES_USER`
   del contenedor y owner de todos los schemas. Los owners de PostgreSQL no
   pueden ser despojados de sus propios objetos con `REVOKE`. La opción
   solo sería viable si se crease un rol de aplicación sin privilegio de
   owner, lo que requiere reestructurar el modelo de roles de la base — fuera
   del alcance de este sprint y sin beneficio proporcional al costo.

---

## Decisión

Se implementan **dos capas de enforcement independientes y complementarias**:

### Capa 1 — Guard en `AuditoriaInterceptor` (defensa en profundidad C#)

Se extrae un método privado `VerificarInmutabilidadAuditoria(DbContext ctx)`
invocado desde **dos overrides**:

- `SavingChangesAsync` (ya existente, llamadas asíncronas)
- `SavingChanges` (**override nuevo, síncrono**) — cierra el gap: si algún
  código llama `SaveChanges()` sin `await`, el guard debe disparar igualmente.

El método itera el `ChangeTracker` buscando `AuditoriaEntidad` con estado
`Modified` o `Deleted`. Si encuentra alguna, lanza `InvalidOperationException`
antes de continuar con el bucle de generación de registros.

```csharp
private static void VerificarInmutabilidadAuditoria(DbContext ctx)
{
    foreach (var entry in ctx.ChangeTracker
        .Entries<AuditoriaEntidad>()
        .Where(e => e.State is EntityState.Modified or EntityState.Deleted))
    {
        throw new InvalidOperationException(
            $"Violación del invariante append-only: intento de {entry.State} " +
            $"sobre AuditoriaEntidad id={entry.Entity.Id}. " +
            $"Los registros de auditoría son inmutables (ADR 0041/0044).");
    }
}
```

Esta capa protege contra errores de programación dentro del proceso. Falla
rápido y con mensaje diagnóstico claro antes de cualquier viaje a la base.

### Capa 2 — Trigger PostgreSQL BEFORE ... FOR EACH STATEMENT (enforcement en DB)

Migración `M009` crea una función y un trigger en `auditoria.auditoria`.
El trigger es **statement-level** (`FOR EACH STATEMENT`) y cubre también
`TRUNCATE`:

```sql
CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION
        'Violación append-only: operación % sobre auditoria.auditoria prohibida (ADR 0044).',
        TG_OP;
END;
$$;

CREATE TRIGGER trg_auditoria_immutable
BEFORE UPDATE OR DELETE OR TRUNCATE ON auditoria.auditoria
FOR EACH STATEMENT EXECUTE FUNCTION auditoria.fn_auditoria_immutable();
```

**Por qué statement-level y por qué TRUNCATE:**

- `TRUNCATE` **bypasea triggers `FOR EACH ROW`** en PostgreSQL — un
  `TRUNCATE auditoria.auditoria` con trigger row-level borraría toda la
  tabla de auditoría en silencio. Statement-level es el único nivel que
  intercepta TRUNCATE.
- Un trigger `FOR EACH STATEMENT` dispara **exactamente una vez por
  sentencia**, incluso si la sentencia afecta 0 filas. El comportamiento
  es correcto: una sentencia `UPDATE ... WHERE false` también es una
  violación del invariante y debe bloquearse.
- Menor costo que row-level: una invocación por sentencia en lugar de
  una por fila afectada.

`M009` incluye `Down()` con `DROP TRIGGER` y `DROP FUNCTION`:

```sql
-- Down()
DROP TRIGGER IF EXISTS trg_auditoria_immutable ON auditoria.auditoria;
DROP FUNCTION IF EXISTS auditoria.fn_auditoria_immutable();
```

Esta capa protege contra:
- SQL crudo desde psql, scripts de mantenimiento o herramientas externas.
- Futuros consumidores del `DbContext` que pasen por alto el guard de Capa 1.
- Cualquier bypass de EF Core (`ExecuteSqlRawAsync`, migraciones con UPDATE accidental).
- TRUNCATE accidental o malicioso sobre la tabla de auditoría.

El trigger es BEFORE, por lo que la operación se cancela antes de afectar
ninguna fila y la transacción queda en estado rollbackable.

---

## Alternativas descartadas

| Alternativa | Razón del descarte |
|---|---|
| Solo REVOKE | `sg_admin` es owner; los owners no pueden ser restringidos con REVOKE sobre sus propios objetos en PostgreSQL. Requeriría rediseño del modelo de roles. |
| Solo guard en EF Core (Capa 1) | No cubre SQL crudo ni acceso directo a la base desde herramientas externas. EF Core puede ser bypasseado con `ExecuteSqlRawAsync`. |
| Solo trigger (Capa 2) | El error llega tarde (viaje de red hasta la DB) y la excepción es opaca para el dominio. El guard en C# falla más rápido y con mensaje más informativo para el desarrollador. |
| Row Security Policy (RLS) | Requiere un rol de aplicación sin `BYPASSRLS` y restructuración del modelo de permisos. Complejidad desproporcionada para el escenario actual (un solo rol de aplicación, municipio único). |

---

## Consecuencias

### Operativas inmediatas
- Todo intento de UPDATE, DELETE o TRUNCATE sobre `auditoria.auditoria` falla
  con excepción — tanto desde la aplicación como desde psql.
- Los tests de integración que hoy usan `EnsureDeletedAsync()` (que hace
  `DROP SCHEMA ... CASCADE`) continúan funcionando porque DROP TABLE no
  es UPDATE ni DELETE sobre filas. El trigger no interviene en DDL.
- El fixture de integración hace `TRUNCATE dominio.predios, dominio.propietarios
  RESTART IDENTITY CASCADE`. Este TRUNCATE **no alcanza `auditoria.auditoria`**
  porque no existe ninguna FK que apunte desde `auditoria.auditoria` a esas
  tablas. Verificado por inspección directa el 2026-06-11:
  - `\d auditoria.auditoria` muestra únicamente PK (`pk_auditoria`) y 2 índices.
  - Ninguna configuración EF Core (`AuditoriaConfiguration.cs`) declara
    `HasForeignKey` sobre `AuditoriaEntidad`.
  - El campo `usuario_id` en `auditoria.auditoria` es `uuid` sin FK declarada
    (decisión deliberada para mantener la tabla de auditoría desacoplada de
    `identidad.usuarios`).
  - La keyword `CASCADE` en el TRUNCATE solo propaga a tablas con FK apuntando
    a las truncadas; `auditoria.auditoria` no tiene ninguna.

### Restricción operativa futura (registrar en item 12 — manual de operación)
**La purga o retención de registros de auditoría** (por cumplimiento normativo,
por crecimiento de tabla, por migración de datos históricos) requerirá:

1. Deshabilitar el trigger temporalmente como superusuario:
   ```sql
   ALTER TABLE auditoria.auditoria DISABLE TRIGGER trg_auditoria_immutable;
   -- operación de purga/retención
   ALTER TABLE auditoria.auditoria ENABLE TRIGGER trg_auditoria_immutable;
   ```
2. Registro en log de auditoría operativa (fuera de la tabla `auditoria.auditoria`)
   de quién, cuándo y por qué motivo se deshabilitó el trigger.
3. Esta operación requiere privilegio de superusuario o de owner de la tabla.
   En el modelo de despliegue local (un solo operador), `sg_admin` cumple.
   En modelo servidor multi-usuario, debe restringirse a DBA designado.

A la fecha de este ADR no existe ningún proceso legítimo previsto de
modificación o borrado de registros de auditoría (no hay obligación
normativa vigente de purga o anonimización aplicable a esta tabla).
El procedimiento DISABLE TRIGGER descrito arriba es exclusivamente de
emergencia/mantenimiento extraordinario. Si una norma futura exigiera
purga o anonimización, esa decisión se documentará en un nuevo ADR que
supersederá parcialmente a este.

### Tests
Se agregan tests de integración en `Auditoria/AuditoriaInterceptorTests.cs`:

**Test 1 — Guard C# (Modified):**
`Add()` produce estado `Added`, no `Modified`, por lo que no puede usarse
para validar el guard. El test correcto es:
1. Disparar una operación auditada real (ej. crear un Predio vía endpoint o
   handler) para que `AuditoriaInterceptor` genere e inserte un
   `AuditoriaEntidad` en la BD.
2. Consultar ese registro con `db.Set<AuditoriaEntidad>().FindAsync(id)` —
   EF Core lo adjunta al `ChangeTracker` en estado `Unchanged`.
3. Mutar una propiedad (ej. `entry.Property("Resultado").CurrentValue = "X"`),
   dejándolo en estado `Modified`.
4. Invocar `db.SaveChangesAsync()` — debe lanzar `InvalidOperationException`
   con el mensaje del guard (ADR 0044).

**Test 2 — Guard C# (Deleted):**
Igual que Test 1, pasos 1-2, luego `db.Set<AuditoriaEntidad>().Remove(entidad)`.
`SaveChangesAsync()` debe lanzar `InvalidOperationException`.

**Test 3 — Trigger DB (UPDATE directo):**
`await db.Database.ExecuteSqlRawAsync("UPDATE auditoria.auditoria SET resultado='TAMPERED' WHERE id=...");`
debe lanzar excepción de Npgsql cuyo mensaje contenga `'Violación append-only'`.
Este test no pasa por EF Core y verifica Capa 2 de forma independiente de Capa 1.

**Test 4 — Trigger DB (TRUNCATE):**
`await db.Database.ExecuteSqlRawAsync("TRUNCATE auditoria.auditoria;");`
debe lanzar excepción de Npgsql. Verifica que el trigger statement-level
protege contra TRUNCATE (que los triggers row-level no interceptarían).

**Test 5 — Existencia del trigger:**
```sql
SELECT COUNT(*) FROM information_schema.triggers
WHERE trigger_schema = 'auditoria'
  AND event_object_table = 'auditoria'
  AND trigger_name = 'trg_auditoria_immutable';
```
El resultado debe ser ≥ 1 (trigger puede aparecer una vez por evento: UPDATE,
DELETE, TRUNCATE → hasta 3 filas en `information_schema.triggers`).

**Test 6 — Regresión:**
El test existente `AuditoriaInterceptorGeometriaTests` debe continuar verde:
el guard no afecta `Added` (INSERTs), solo `Modified` y `Deleted`.

---

## Archivos a modificar (pendiente aprobación)

| Archivo | Cambio |
|---|---|
| `SG.Infrastructure/Persistencia/Interceptors/AuditoriaInterceptor.cs` | Método `VerificarInmutabilidadAuditoria` invocado desde `SavingChangesAsync` y `SavingChanges` (override nuevo) |
| `SG.Infrastructure/Persistencia/Migrations/M009_*.cs` | Función + trigger en `auditoria.auditoria` |
| `tests/SG.Api.IntegrationTests/Auditoria/AuditoriaInterceptorTests.cs` | Tests nuevos (guard C# + trigger DB + existencia) |

---

## Referencias

- ADR 0041 — Auditoría append-only independiente (invariante declarado)
- ADR 0043 — Entorno canónico Docker (contexto de despliegue)
- `AuditoriaConfiguration.cs` — `builder.ToTable("auditoria", schema: "auditoria")`
- Diagnóstico 2026-06-11: cero filas en `information_schema.triggers` para `auditoria.auditoria`
