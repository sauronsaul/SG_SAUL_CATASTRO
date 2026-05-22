# ADR 0033 — EF Core: INSERT vs UPDATE en entidades hijas — `ValueGeneratedNever()`

**Estado**: Aceptado  
**Fecha**: 2026-05-19  
**Sprint**: 2 / Checkpoint 2.2 (Bug 2)  

---

## Contexto (Postmortem Bug 2)

Al implementar el endpoint `PUT /api/predios/{id}/propietario` (vincular propietario),
EF Core lanzaba `DbUpdateConcurrencyException` al ejecutar `SaveChangesAsync`.

### Síntoma

```
DbUpdateConcurrencyException: The database operation was expected to affect 1 row(s),
but actually affected 0 row(s); the data may have been modified or deleted since entities
were loaded.
```

### Causa raíz

EF Core infiere la operación (INSERT vs UPDATE) según si la entidad es "nueva" desde
su perspectiva. Para entidades con PK de tipo `Guid`, EF Core asume que si la PK tiene
un valor no-default (`Guid.Empty`), la entidad **ya existe** en la base de datos y emite
`UPDATE` en vez de `INSERT`.

Las entidades hijas del agregado `Predio` generan su propia PK en el constructor
de dominio (vía `Guid.NewGuid()`):

- `RelacionPredioPropietario` → `Id = Guid.NewGuid()`
- `Documento` → `Id = Guid.NewGuid()`
- `HistorialEstado` → `Id = Guid.NewGuid()`

Sin `ValueGeneratedNever()` en la configuración EF Core, el contexto interpretaba
estas PKs autogeneradas como señal de que la entidad existía, emitiendo `UPDATE 0 rows`
y disparando la excepción de concurrencia.

### Fix aplicado

En cada `IEntityTypeConfiguration<T>` de las entidades hijas se agregó:

```csharp
builder.Property(x => x.Id).ValueGeneratedNever();
```

Esto le indica a EF Core que la aplicación es responsable de generar el valor de
la PK (no la base de datos), y que una PK con valor no-default en una entidad no
rastreada implica INSERT, no UPDATE.

## Decisión

**Toda entidad del dominio cuya PK se genere en el constructor de C# debe declarar
`ValueGeneratedNever()` en su configuración EF Core.**

Esto aplica a todas las entidades que no sean raíces de agregado gestionadas
directamente por el `DbSet<T>`.

### Regla de verificación

Al añadir una nueva entidad hija (que no sea el agregado raíz), verificar en su
`IEntityTypeConfiguration<T>`:

```csharp
builder.Property(x => x.Id).ValueGeneratedNever();  // PK generada en dominio
```

## Test de regresión

`VincularPropietario_PersisteLaRelacion_RegresionBug2` en
`SG.Api.IntegrationTests/Catastro/PredioE2ETests.cs`:

```csharp
// Este test cazaría el DbUpdateConcurrencyException del Bug 2 si reapareciera.
// Causa: EF Core emitía UPDATE en vez de INSERT por falta de ValueGeneratedNever()
// en las PKs de entidades hijas (RelacionPredioPropietario, Documento, HistorialEstado).
```

El test verifica explícitamente que la fila fue INSERTADA (no actualizada) en la BD,
usando `db.RelacionesPredioPropietario.Where(r => r.PredioId == predioId).ToListAsync()`.

## Consecuencias

- El bug está corregido y cubierto por test de regresión de integración.
- La convención `ValueGeneratedNever()` para entidades hijas queda documentada
  como práctica obligatoria del proyecto.
- Al agregar nuevas entidades hijas en sprints posteriores (Construccion,
  Colindancia, Certificado), verificar esta configuración antes de cerrar el PR.

## Alternativas descartadas

| Alternativa | Motivo |
|---|---|
| Generación de PK en BD (`serial` / `gen_random_uuid()`) | Rompe el invariante de dominio: la identidad del objeto debe existir antes de persistir |
| `EntityState` manual antes de `SaveChanges` | Frágil, acoplado a EF Core en la capa de dominio |
| Detectar entidades nuevas por ausencia en `ChangeTracker` | Complejo y propenso a bugs en escenarios de reconexión |
