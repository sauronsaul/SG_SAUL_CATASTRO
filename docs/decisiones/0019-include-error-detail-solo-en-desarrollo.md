# ADR 0019 — Include Error Detail habilitado solo en desarrollo

**Fecha**: 2026-05-12
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

Npgsql soporta la opción `Include Error Detail=true` en el connection string, que
hace que PostgreSQL devuelva mensajes de error detallados (columnas, valores, constraints)
en las excepciones de Npgsql. Esto es muy útil en desarrollo para depurar migraciones
y consultas, pero en producción expone información sensible del esquema de base de datos
a quien intercepte errores.

Durante el Checkpoint 1.2, `Include Error Detail=true` estaba en la variable de entorno
`ConnectionStrings__Default` dentro de `.env`. El problema es que en ASP.NET Core 10,
las variables de entorno tienen **mayor prioridad** que `appsettings.json` y
`appsettings.Production.json`. Si `.env` se carga en un servidor de producción
(intencional o accidentalmente), `Include Error Detail` llegaría al servidor.

## Decisión

`Include Error Detail` **NO se configura en `.env` ni en el connection string**.

Se controla mediante la clave de configuración `Npgsql:IncludeErrorDetail` en
`appsettings.Development.json`, que:
- Solo existe en el entorno `Development`.
- No se versiona en el servidor (ASP.NET Core no carga `appsettings.Development.json`
  salvo que `ASPNETCORE_ENVIRONMENT=Development`).
- Nunca llega a producción por diseño.

### Implementación

**`appsettings.Development.json`**:
```json
{
  "Npgsql": {
    "IncludeErrorDetail": true
  }
}
```

**`SG.Infrastructure/Persistencia/DependencyInjection.cs`**:
```csharp
if (configuration.GetValue<bool>("Npgsql:IncludeErrorDetail"))
    connectionString += ";Include Error Detail=true";
```

**`.env` / `.env.example`** — `ConnectionStrings__Default` **no incluye**
`Include Error Detail=true`. La clave solo vive en `appsettings.Development.json`.

## Consecuencias

**Positivas**:
- `Include Error Detail` es imposible de activar en producción sin modificar código.
- El connection string de `.env` es limpio y portable entre entornos.
- La decisión es rastreable y auditable (este ADR).

**Negativas / compromisos**:
- Un desarrollador que no lea este ADR podría buscar `Include Error Detail` en `.env`
  y no encontrarlo. El comentario en `.env.example` guía hacia este documento.
