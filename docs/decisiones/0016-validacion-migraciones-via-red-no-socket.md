# ADR 0016 — Validación de migraciones: solo vía red, nunca vía socket

**Fecha**: 2026-05-12
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

Durante el Checkpoint 1.2, `dotnet ef database update` falló con error de
autenticación. La migración M001 se aplicó como workaround usando:

```bash
docker exec sg_postgres psql -U sg_admin -d sg_catastro -f /tmp/M001.sql
```

Esta conexión usa el socket Unix dentro del contenedor, que en la configuración
por defecto de Docker no requiere password (`trust`). La migración se aplicó
correctamente a nivel de esquema, pero el flujo real de la aplicación (.NET →
TCP → PostgreSQL con password) **nunca fue validado**.

El problema real (password con `;` en la connection string) quedó enmascarado
y fue detectado después.

## Decisión

**Las migraciones se validan ÚNICAMENTE mediante `dotnet ef database update`
o `dotnet run` — nunca mediante `psql` desde dentro del contenedor.**

Aplicar SQL directamente con `psql` es un workaround que:
1. Omite la autenticación real que usa la aplicación.
2. Omite la lógica de `IDesignTimeDbContextFactory` (carga de .env, validaciones).
3. Da un falso positivo: la BD puede quedar en el estado correcto mientras el
   código de la aplicación sigue roto.

Si `dotnet ef database update` falla, el fallo **es información valiosa** — hay
que resolverlo, no rodearlo.

## Flujo correcto de validación

```
dotnet ef database update --project SG.Infrastructure --startup-project SG.Api
  └── ApplicationDbContextFactory.CreateDbContext()
        └── CargarDotEnv()             ← valida que .env existe y es parseable
        └── ConnectionStrings__Default  ← valida que la connection string es correcta
        └── UseNpgsql(connectionString) ← valida que Npgsql puede parsearla
  └── NpgsqlMigrator.Migrate()
        └── Abre conexión TCP a localhost:5432
        └── Autentica con scram-sha-256 ← valida password real
        └── Aplica DDL
        └── Inserta en __ef_migrations_history
```

Si cualquier paso falla, el error se reporta al desarrollador — que es
exactamente lo que debe ocurrir.

## Regla operativa

| Acción | ¿Permitida para validar? |
|---|---|
| `dotnet ef database update` | ✅ Sí — es el flujo real |
| `dotnet run` (aplica migraciones en startup) | ✅ Sí — si se implementa |
| `psql -f migration.sql` desde host (con password) | ⚠️ Solo como último recurso documentado |
| `docker exec psql -f ...` sin password | ❌ No — falso positivo, enmasca errores |

## Excepción documentada: Checkpoint 1.2

En el Checkpoint 1.2 se usó `docker exec psql` como workaround de emergencia
porque el diagnóstico del error real (password con `;`) requería confirmar
que el esquema era correcto antes de proceder. **No es el flujo estándar.**

La corrección posterior (password sin caracteres especiales + pg_hba.conf
endurecido) elimina la posibilidad de usar este workaround accidentalmente.

## Consecuencias

**Positivas**:
- Las migraciones solo se consideran válidas si el flujo completo funciona.
- Los errores de connection string se detectan en desarrollo, no en producción.
- `pg_hba.conf` endurecido (ADR 0015) hace imposible el workaround por socket.

**Negativas / compromisos**:
- Requiere que `ConnectionStrings__Default` en `.env` sea siempre correcta.
  Un password roto bloquea el desarrollo — que es la señal correcta.
