# ADR 0034 — Database.SqlQuery<bool> para VOs con HasConversion y corrección de connection string leak en SgApiFactory

**Estado**: Aceptado  
**Fecha**: 2026-05-21  
**Sprint**: 2 / Checkpoint 2.3  

---

## Contexto

Al implementar y verificar los tests de integración del Checkpoint 2.3 se tomaron
dos decisiones técnicas en la capa de infraestructura: una de diseño deliberado
al escribir `ExisteCodigoCatastralAsync`, y una corrección de un bug real en la
factory de tests. Se documentan juntas por pertenecer al mismo checkpoint.

---

## Sección 1 — Decisión de diseño: `Database.SqlQuery<bool>` para columnas VO con `HasConversion`

### Contexto específico

`PredioRepositorio.ExisteCodigoCatastralAsync` debe comprobar si ya existe en la
BD un predio con un `CodigoCatastral` determinado. La columna `codigo_catastral`
está mapeada con `HasConversion`:

```csharp
builder.Property(x => x.CodigoCatastral)
    .HasConversion(
        v => v != null ? v.Valor : null,
        v => v != null ? CodigoCatastral.FromDb(v) : null);
```

### Por qué LINQ AnyAsync no es la solución correcta

EF Core no puede traducir comparaciones que involucran un Value Object con
`HasConversion` cuando el VO aparece en el lado izquierdo de la expresión:

```csharp
// NO recomendado: EF Core genera SQL incorrecto o lanza excepción en runtime
await db.Predios.AnyAsync(p => p.CodigoCatastral == voResult.Value, ct);
```

EF Core intentaría comparar el objeto VO en memoria con la columna string de la
BD, generando SQL que puede omitir el soft-delete filter, aplicar la comparación
de forma incorrecta, o simplemente no traducirse.

### Decisión: `Database.SqlQuery<bool>` con `FormattableString`

```csharp
var codigoStr = voResult.Value.Valor;
return await db.Database
    .SqlQuery<bool>(
        $"SELECT EXISTS(SELECT 1 FROM dominio.predios WHERE codigo_catastral = {codigoStr} AND NOT is_deleted) AS \"Value\"")
    .FirstAsync(ct);
```

`FormattableString` garantiza que el valor de `codigoStr` se envíe como parámetro
`@p0` (no interpolado en el SQL), previniendo inyección y asegurando el tipo correcto
en PostgreSQL. EF Core envuelve la consulta como:

```sql
SELECT s."Value"
FROM (SELECT EXISTS(SELECT 1 FROM dominio.predios
      WHERE codigo_catastral = @p0 AND NOT is_deleted) AS "Value") AS s
LIMIT 1
```

El filtro `AND NOT is_deleted` reemplaza explícitamente el query filter global de
`HasQueryFilter` (que no aplica en `SqlQuery<T>`).

### Regla derivada

Cuando una columna mapeada con `HasConversion` (VO ↔ tipo primitivo) participa
en una consulta de existencia o unicidad, usar `Database.SqlQuery<T>` con
`FormattableString` en lugar de LINQ. La alternativa de extraer el valor primitivo
y compararlo con `.Select(p => p.CodigoCatastral!.Valor).AnyAsync(v => v == codigoStr)`
también funciona pero es menos directa que la consulta SQL explícita para este
caso de unicidad simple.

---

## Sección 2 — Corrección de bug: connection string leak en `SgApiFactory`

### Causa raíz

`Program.cs` llama a `AddPersistencia(builder.Configuration)` durante el arranque
de la aplicación. Dentro de `AddPersistencia` (en `DependencyInjection.cs`), la
connection string se captura en una **variable local** antes de registrar el
`DbContext`:

```csharp
var connectionString = configuration.GetConnectionString("Default");
// ...
services.AddDbContext<ApplicationDbContext>((sp, options) =>
    options.UseNpgsql(connectionString, ...));  // ← variable local capturada
```

`WebApplicationFactory.ConfigureWebHost` aplica `ConfigureAppConfiguration` (con
el `InMemoryCollection` de Testcontainers) **después** de que `Program.cs` ya
ejecutó. Si `DotNetEnv.Env.Load(.env)` en `Program.cs` sobreescribió la variable
de entorno `ConnectionStrings__Default` (que `PostgreSqlFixture.InitializeAsync`
había seteado al string de Testcontainers), `AddPersistencia` captura el string
del `.env` local y el `DbContext` apunta a la BD de desarrollo, no al contenedor.

### Síntoma observable

Los tests `FlujCompleto` y `CodigoCatastralDuplicado` retornaban 409 cuando
debían retornar 204 porque `ExisteCodigoCatastralAsync` consultaba la BD local
(que ya tenía datos de pruebas anteriores) en lugar del contenedor Testcontainers
recién inicializado.

### Corrección aplicada

En `SgApiFactory.ConfigureWebHost`, reemplazar el `DbContextOptions` registrado
por `AddPersistencia` con uno nuevo que use la connection string del contenedor:

```csharp
services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
services.AddDbContext<ApplicationDbContext>((sp, options) =>
{
    options.UseNpgsql(_connectionString, npgsql =>
    {
        npgsql.UseNetTopologySuite();
        npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "identidad");
    });
    options.UseSnakeCaseNamingConvention();
    options.AddInterceptors(
        sp.GetRequiredService<AuditableEntitiesInterceptor>(),
        sp.GetRequiredService<AuditoriaInterceptor>());
});
```

Este patrón es el estándar de `WebApplicationFactory` para reemplazar servicios
cuya configuración fue capturada antes del override.

### Nota: doble interceptor en tests

`AddPersistencia` registra los interceptores; `SgApiFactory` los vuelve a registrar
al re-declarar el `DbContext`. Los interceptores se ejecutan dos veces por
`SaveChanges`, pero sin consecuencias adversas:
- `AuditableEntitiesInterceptor` es idempotente (solo setea fechas si son default).
- `AuditoriaInterceptor` genera `Guid.NewGuid()` por registro: el duplicado crea
  una fila extra de auditoría, no un error funcional.

Si en el futuro `AddPersistencia` acepta la connection string como parámetro
directo (en vez de leerla de `IConfiguration`), el bloque `RemoveAll + AddDbContext`
puede eliminarse y este ADR debe actualizarse.

---

## Consecuencias

- `ExisteCodigoCatastralAsync` usa `Database.SqlQuery<bool>` como patrón estándar
  para consultas de existencia sobre columnas con `HasConversion`.
- `SgApiFactory` mantiene el bloque `RemoveAll + AddDbContext` con comentario
  explicativo. No se elimina aunque parezca verboso.
- Los 14 tests de integración pasan en verde con 0 errores y 0 warnings.
