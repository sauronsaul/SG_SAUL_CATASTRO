# Sprint 1 — Checkpoint 1.3: Resumen de cierre

**Fecha**: 2026-05-15
**Autor**: Saul Gutierrez
**Estado**: Completado

---

## Objetivo del checkpoint

Completar el módulo de autenticación JWT con refresh token rotation, auditoría completa de eventos de identidad, y tests unitarios de los handlers.

---

## Entregables completados

### Infraestructura de seguridad
- `BcryptPasswordHasher` — reemplaza el hasher PBKDF2 de ASP.NET Identity con BCrypt work factor 12.
- `JwtTokenService` — genera access tokens (HS256, 15 min) y refresh tokens (64 bytes criptográficos).
- `RefreshTokenRepositorio` — crear, buscar, revocar, revocar-todos. Detección de reutilización automática.
- `UsuarioServicio` — búsqueda por email/id, verificación de password, registro de accesos fallidos, lockout.
- `AuditoriaService` — escritura de registros en `auditoria.auditoria` con todos los campos requeridos.

### Migración de base de datos
- **M002** (`20260514141927`): agrega columna `revoked_reason varchar` en `identidad.refresh_tokens`.
- Valores posibles: `rotacion`, `reutilizacion_detectada`, `usuario_eliminado` (Sprint 2).

### Casos de uso (SG.Application)
- `LoginCommand` — autenticación con auditoría de todos los casos de fallo.
- `LogoutCommand` — revocación idempotente del refresh token activo.
- `RefreshTokenCommand` — rotación de token con detección de reutilización.
- `ObtenerUsuarioActualQuery` — endpoint `/me` para frontend.

### API REST
- `AuthController` — endpoints `POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`, `GET /api/auth/me`.
- `Program.cs` — pipeline JWT Bearer, FluentValidation, MediatR, IdentitySeeder en startup.

### Seeder
- `IdentitySeeder` — crea usuario admin y roles base (Admin, Tecnico, Operador, Consulta) en primer arranque. Fail-fast si `ADMIN_EMAIL` o `ADMIN_PASSWORD` no están configurados.

### Tests
- `LoginCommandHandlerTests` — 5 casos: usuario inexistente, cuenta bloqueada, password incorrecto, lockout en 5to intento, login exitoso.
- `RefreshTokenCommandHandlerTests` — 4 casos: token inexistente, expirado, reutilización detectada, rotación exitosa.
- **Total: 11 tests verdes** (9 de Application + 1 de Domain + 1 de Integration).

---

## Correcciones de calidad realizadas durante el checkpoint

| Problema | Corrección |
|----------|-----------|
| MediatR 14.x requiere licencia comercial | Downgrade a 12.5.0 (MIT) — commit `5fb1b98` |
| `revoked_reason` y `accion` en PascalCase | Normalizado a snake_case en todos los handlers |
| Módulo de auditoría como `"Autenticacion"` | Cambiado a `"identidad"` (nombre del schema PostgreSQL) |
| `login_fallido` sin `motivo` en password incorrecto | Agregado `motivo: "password_incorrecto"` |
| `logout` sin auditoría cuando token no existe | Agregado bloque `else` con auditoría y `motivo: "token_inexistente"` |
| `JwtBearer` en `SG.Infrastructure.csproj` (viola Clean Architecture) | Reemplazado por `System.IdentityModel.Tokens.Jwt` y `Microsoft.IdentityModel.Tokens` |

---

## Decisiones técnicas documentadas

| ADR | Título |
|-----|--------|
| 0011 | Convención de nombres de módulo en auditoría (`modulo` = schema PostgreSQL en minúsculas) |
| 0025 | Soft-delete de UsuarioIdentidad y revocación de RefreshTokens (deuda controlada Sprint 2) |
| 0026 | MediatR 12.5.0 en lugar de 14.x (licencia MIT) |

---

## Deuda técnica registrada para Sprint 2

- **Eliminar usuario**: cuando se implemente `EliminarUsuarioCommand`, debe revocar todos los refresh tokens activos antes de aplicar `IsDeleted = true` (ver ADR 0025).
- **EF Core warning**: `UsuarioIdentidad` tiene query filter soft-delete con relación requerida a `RefreshToken` — benigno hasta Sprint 2.
- **Roles finos por claim**: roles base creados, permisos finos pendientes.
- **Endpoint de registro**: solo admin crea usuarios (Sprint 3 o posterior).

---

## Estado de los criterios de aceptación

| Criterio | Estado |
|----------|--------|
| `POST /api/auth/login` → 200 con JWT + refresh | ✓ |
| `POST /api/auth/refresh` → rotación correcta | ✓ |
| `POST /api/auth/logout` → 200 idempotente | ✓ |
| `GET /api/auth/me` → datos del usuario autenticado | ✓ |
| Lockout en 5 intentos → 423 | ✓ |
| Reutilización detectada → todos los tokens revocados | ✓ |
| Auditoría en todos los flujos (OK y ERROR) | ✓ |
| Tests unitarios ≥ 9 casos | ✓ (9 casos) |
| Build 0 errores 0 warnings | ✓ |
| `dotnet test` 100% verde | ✓ |
