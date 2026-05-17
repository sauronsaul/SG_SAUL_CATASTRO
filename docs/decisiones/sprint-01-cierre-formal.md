# Sprint 1 — Cierre Formal

**Fecha de cierre**: 2026-05-16
**Autor**: Saul Gutierrez
**Tag de release**: `v0.1.0-mvp-backend`

---

## Objetivo del Sprint 1

Construir el esqueleto de backend con autenticación JWT completa, pipeline de CI/CD básico,
infraestructura Docker productiva y cobertura de tests suficiente para garantizar la calidad
del módulo de seguridad antes de avanzar al dominio catastral.

**Entregable verificable**: Login JWT funcional en contenedor Docker + suite de tests completa.

---

## Tabla consolidada de checkpoints

| Checkpoint | Entregables principales | ADRs creados |
|---|---|---|
| **1.1** — Skeleton + Identity | Proyectos C#, EF Core, ASP.NET Identity, migraciones M001, DotNetEnv, Docker Compose base (postgres + minio) | 0001, 0005, 0006, 0007 |
| **1.2** — Infraestructura seguridad | `AuditoriaInterceptor`, `BcryptPasswordHasher`, entidades `UsuarioIdentidad` + `RefreshToken`, migraciones M001b, pg_hba scram-sha-256, Caddy reverse proxy | 0011, 0012, 0013, 0014, 0015, 0016, 0017, 0018, 0019 |
| **1.3** — Auth JWT completa | `JwtTokenService`, `UsuarioServicio`, `RefreshTokenRepositorio`, `AuditoriaService`, handlers Login/Logout/Refresh/Me, `AuthController`, `IdentitySeeder`, migración M002 | 0025, 0026 |
| **1.4** — Tests + Docker productivo | 45 tests (17 + 20 + 8), Dockerfile multi-stage 222 MB, docker-compose integrado, docs operativas, `.dockerignore`, 5 pruebas E2E manuales aprobadas | 0027, 0028, 0029 |

---

## Estado del sistema al cierre del Sprint 1

### Servicios Docker en modo local

| Servicio | Imagen | Estado | Puerto |
|---|---|---|---|
| `sg_postgres` | `postgis/postgis:16-3.4-alpine` | healthy | 5434 |
| `sg_minio` | `minio/minio:latest` | healthy | 9000–9001 |
| `sg_api` | `sg-catastro-api` (222 MB) | healthy | configurable |
| `sg_caddy` | `caddy:2-alpine` | running | 80, 443 |

### Endpoints de autenticación operativos

| Endpoint | Descripción | Estado |
|---|---|---|
| `POST /api/auth/login` | Login con email + password, retorna JWT + refresh token | ✓ |
| `POST /api/auth/refresh` | Rotación de refresh token con detección de reutilización | ✓ |
| `POST /api/auth/logout` | Revocación del refresh token activo | ✓ |
| `GET /api/auth/me` | Datos del usuario autenticado (requiere JWT válido) | ✓ |
| `GET /health` | Health check del servicio (EF Core + DB) | ✓ |

### Suite de tests

| Proyecto | Tests | Duración |
|---|---|---|
| `SG.Domain.Tests` | 17 | ~120 ms |
| `SG.Application.Tests` | 20 | ~250 ms |
| `SG.Api.IntegrationTests` | 8 E2E | ~15 s |
| **Total** | **45** | **~16 s** |

### Cobertura efectiva

- **SG.Domain**: 76.7% reportado / ~98% efectivo en clases con lógica real (ADR 0027).
- **SG.Application**: handlers y validators cubiertos al 100% por tests unitarios y E2E.

### ADRs documentados en Sprint 1

`0001`, `0005`–`0007`, `0011`–`0019`, `0025`–`0029` — 20 decisiones técnicas registradas.

---

## Pendientes confirmados para Sprint 2

### Dominio catastral (Sprint 2 — prioridad máxima)

| Tarea | Descripción |
|---|---|
| Entidad `Predio` | Agregado raíz del catastro con `CodigoCatastral` (Value Object) |
| Value Object `CodigoCatastral` | Formato Caranavi `2-04-ZZZ-MMM-LLL`, validación, parseo, normalización |
| Entidad `Propietario` | Persona natural o jurídica, cédula, NIT |
| `RelacionPredioPropietario` | Titularidad con historial temporal |
| `UbicacionCatastral` | Coordenadas, zona, manzana, lote |
| Migraciones de dominio | Tablas `predios`, `propietarios`, `relaciones_predio_propietario` |
| Tests de dominio | Cobertura ≥ 80% reportado (nuevas entidades y VOs con lógica real) |

### Deuda técnica aceptada

| Item | ADR / Referencia | Sprint objetivo |
|---|---|---|
| Cobertura Domain 76.7% reportado | ADR 0027 — subirá con Predio/Propietario | Sprint 2 (natural) |
| `EliminarUsuarioCommand` con revocación de tokens | ADR 0025 | Sprint 3 |
| Permisos finos por claim | CLAUDE.md sección 10 | Sprint 3 |
| Multi-instance migration risk | ADR 0029 | Sprint 7 (modo servidor) |
| Endpoint de registro de usuarios | Solo admin, no autoregistro | Sprint 3 |

---

## Criterio de cierre del Sprint 1

El Sprint 1 se cierra **APROBADO** con todos los criterios de aceptación del entregable
verificable cumplidos:

- ✓ Login JWT funcional en contenedor Docker
- ✓ Suite de tests completa (45/45) incluyendo E2E
- ✓ Sistema instalable en una PC con `bash scripts/start-local.sh`
- ✓ Documentación operativa lista para operador municipal

**Próximo sprint**: Sprint 2 — Dominio catastral (`Predio`, `Propietario`, `CodigoCatastral`).
