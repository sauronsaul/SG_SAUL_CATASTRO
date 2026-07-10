# ADR 0048 — Modelo híbrido de desarrollo: nativo para iteración, contenedor para paridad

**Fecha**: 2026-07-10
**Estado**: Aceptado
**Autores**: Saul Gutierrez + equipo del proyecto

---

## Contexto

El equipo necesita iterar rápido con la API nativa contra `sg_postgres` en
`localhost:5434`, sin perder la comprobación de que la imagen Docker y Caddy se
comportan como el despliegue local. La carga ascendente de `.env` con DotNetEnv
puede conectar una ejecución nativa a la base canónica, por lo que aplicar
migraciones automáticamente en ese camino es un riesgo operativo.

## Decisión

La iteración se realiza con `dotnet run` y `SG_APPLY_MIGRATIONS` ausente o en
`false`. La API consulta las migraciones pendientes, registra el estado y no
ejecuta `MigrateAsync`.

El servicio `api` de Docker declara `SG_APPLY_MIGRATIONS=true`. Ese es el camino
autorizado para aplicar migraciones durante el arranque containerizado. Antes de
integrar cambios se reconstruye el stack compuesto y se valida `/health` a
través de Caddy.

## Justificación

Separar la iteración de la paridad mantiene el ciclo local rápido y reduce el
riesgo de una migración accidental contra la base canónica. A la vez, la imagen,
la red Docker, MinIO, PostgreSQL y el proxy siguen siendo una verificación
obligatoria y reproducible.

## Consecuencias

- El modo nativo no modifica el esquema salvo autorización explícita.
- El contenedor conserva una ruta controlada de actualización de esquema.
- `docs/DESARROLLO_NATIVO.md` y `docs/SMOKE_TEST.md` son parte del contrato
  operativo.
- ADR 0029 queda acotado: ya no aplica incondicionalmente a todo arranque de la
  API.
