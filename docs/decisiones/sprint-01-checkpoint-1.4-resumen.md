# Sprint 1 — Checkpoint 1.4: Resumen de cierre

**Fecha**: 2026-05-16
**Autor**: Saul Gutierrez
**Estado**: Completado

---

## Objetivo del checkpoint

Completar el ciclo de calidad del Sprint 1: tests de integración E2E con Testcontainers,
Dockerfile multi-stage productivo, integración completa en Docker Compose, y documentación
operativa para despliegue en modo local.

---

## Entregables completados

### Tests (suite completa)

| Proyecto | Tests | Duración |
|---|---|---|
| `SG.Domain.Tests` | 17 | ~120 ms |
| `SG.Application.Tests` | 20 | ~250 ms |
| `SG.Api.IntegrationTests` | 8 | ~15 s |
| **Total** | **45** | **~16 s** |

Cobertura efectiva de dominio: **~98%** en clases con lógica de negocio real
(ver ADR 0027 para criterio de exclusión de clases base abstractas).

**Tests de integración E2E (`AuthE2ETests`):**
- `Login_ConCredencialesValidas_Retorna200ConTokens`
- `Login_ConEmailInexistente_Retorna401`
- `Login_ConPasswordIncorrecto_Retorna401`
- `Login_SinBody_Retorna400`
- `Logout_ConTokenValido_Retorna200`
- `Logout_SinToken_Retorna401`
- `Me_ConTokenValido_Retorna200ConDatosUsuario`
- `Me_SinToken_Retorna401`

### Infraestructura de tests de integración

- `PostgreSqlFixture` — Testcontainer PostgreSQL 16 + PostGIS con schema real, migrations y seed.
- `SgApiFactory` — `WebApplicationFactory` con `PostConfigure<JwtBearerOptions>` para sincronizar
  JWT secret entre composición del host y validación en runtime (ver ADR 0028).

### Docker

- `api.Dockerfile` — build multi-stage: `sdk:10.0` (compilación) + `aspnet:10.0-alpine` (runtime).
  Imagen final: **222 MB**. Incluye ICU, tzdata, usuario no-root `sgapp`, HEALTHCHECK.
- `docker-compose.yml` — servicio `sg_api` integrado con healthcheck, `depends_on: service_healthy`
  sobre postgres y minio, volumen `sg_api_logs`.
- `docker-compose.local.yml` — override `ASPNETCORE_ENVIRONMENT=Development` para Swagger.
- `.dockerignore` — excluye `**/obj/`, `**/bin/`, `.git/`, `docs/` y secrets del contexto de build.
- `scripts/start-local.sh` — arranca el stack completo, detecta `.env` ausente y pausa para edición.

### API — correcciones de startup

- `Program.cs`: `using Microsoft.EntityFrameworkCore` agregado (necesario para `MigrateAsync`).
- Orden de startup: `MigrateAsync → SeedAsync → app.Run()` (ver ADR 0029).

### Documentación operativa

- `docs/operacion/instalacion-modo-local.md` — guía completa para operador municipal.
- `docs/operacion/troubleshooting.md` — 8 escenarios de fallo con diagnóstico y solución.

---

## Bugs detectados y resueltos en este checkpoint

| Bug | Causa raíz | Fix |
|---|---|---|
| Tests E2E → 401 en `GET /me` a pesar de token válido | `Program.cs` captura `jwtSecret` antes de que `WebApplicationFactory` aplique sus overrides de config | `PostConfigure<JwtBearerOptions>` en `SgApiFactory` sincroniza la signing key (ADR 0028) |
| `docker compose up --build` → "failed to read dockerfile" | `dockerfile: api.Dockerfile` se resolvía como `src/backend/api.Dockerfile` (incorrecto) | `context: ../..` + `dockerfile: infra/docker/api.Dockerfile` |
| `docker build` → "Unable to find fallback package folder C:\\Program Files..." | El `obj/` del host Windows contiene assets NuGet con paths Windows; copiados al container Linux | `.dockerignore` con `**/obj/` y `**/bin/` |
| `dotnet publish` → `CS1061: 'DatabaseFacade' no contiene 'MigrateAsync'` | `using Microsoft.EntityFrameworkCore` ausente en `Program.cs` | Agregado el using |

---

## Decisiones técnicas documentadas

| ADR | Título |
|---|---|
| 0027 | Cobertura mínima con criterios de exclusión (80% efectivo vs. reportado) |
| 0028 | Sincronización de JWT Secret en tests de integración con `PostConfigure` |
| 0029 | Migraciones automáticas al arranque de la API (`MigrateAsync` antes de `SeedAsync`) |

---

## Pruebas E2E manuales (Punto B) — 5/5 aprobadas

Realizadas por Saul Gutierrez el 2026-05-16 con el stack completo en Docker:

| Prueba | Endpoint | Resultado |
|---|---|---|
| Login con credenciales correctas | `POST /api/auth/login` | ✓ 200 + access + refresh token |
| Login con credenciales incorrectas | `POST /api/auth/login` | ✓ 401 |
| Acceso a endpoint protegido con token válido | `GET /api/auth/me` | ✓ 200 + datos de usuario |
| Acceso sin token | `GET /api/auth/me` | ✓ 401 |
| Logout | `POST /api/auth/logout` | ✓ 200 idempotente |

---

## Estado de los criterios de aceptación

| Criterio | Estado |
|---|---|
| 45 tests verdes (17 domain + 20 application + 8 integration) | ✓ |
| Build `0 errores 0 advertencias` en Release | ✓ |
| Imagen Docker construida y publicada localmente | ✓ 222 MB |
| `docker compose up` levanta 4 servicios healthy | ✓ postgres, minio, api, caddy |
| `GET /health` → `Healthy` | ✓ |
| `POST /api/auth/login` funciona en contenedor | ✓ |
| Documentación operativa lista para municipio | ✓ |

---

## Deuda técnica para Sprint 2

- Cobertura Domain reportada: 76.7% (efectiva ~98% — ver ADR 0027); subirá naturalmente con Predio/Propietario.
- `[assembly: InternalsVisibleTo]` en `SG.Application/Properties/AssemblyInfo.cs` — correcto para los tests actuales; si los handlers se vuelven `internal`, ya está en su lugar.
- Multi-instance migration risk: aceptado para modo local single-instance (ver ADR 0029).
