# Desarrollo nativo de la API

La API se ejecuta de forma nativa contra PostgreSQL/PostGIS containerizado en
`localhost:5434`. Este modo es para iteración; la validación de paridad se hace
con el contenedor `sg_api` descrito en [SMOKE_TEST.md](SMOKE_TEST.md).

## Arranque seguro

Desde la raíz del repositorio:

```powershell
$env:SG_APPLY_MIGRATIONS = "false"
dotnet run --project src/backend/SG.Api
curl.exe -fsS http://localhost:8080/health
```

El valor ausente de `SG_APPLY_MIGRATIONS` equivale a `false`. Solo el valor
`true` (sin distinguir mayúsculas/minúsculas) autoriza `MigrateAsync`.

## Cadena de conexión y precedencia

`appsettings.Development.json` versiona únicamente esta base, sin contraseña:

```text
Host=localhost;Port=5434;Database=sg_catastro;Username=sg_admin
```

Cuando no existe una cadena completa con `Password` en
`ConnectionStrings__Default`, `Program.cs` agrega la contraseña tomada de
`SG_DB_PASSWORD`. Así la contraseña no se guarda en archivos versionados.

El orden efectivo es:

1. `ConnectionStrings__Default` completa en el entorno, incluida la cargada por
   DotNetEnv desde `.env`.
2. La base de `appsettings.Development.json` más `SG_DB_PASSWORD`.
3. Si no hay una cadena utilizable, la API falla antes de registrar el contexto.

> Advertencia: `Program.cs` busca `.env` desde el directorio actual hacia la
> raíz. Por tanto, en este repositorio todo `dotnet run` nativo carga el `.env`
> raíz y conecta a la base canónica mientras exista su
> `ConnectionStrings__Default`. El guard de migraciones es la protección activa;
> no ejecute con `SG_APPLY_MIGRATIONS=true` salvo que haya revisado la migración
> y la base de destino.

Para usar el fallback de desarrollo en un entorno que no provea la cadena
completa, defina `SG_DB_PASSWORD` antes del arranque:

```powershell
$env:SG_DB_PASSWORD = "<password-no-versionado>"
dotnet run --project src/backend/SG.Api
```

No copie contraseñas a comandos guardados, documentación, commits ni tickets.
