# API de Autenticación

**Módulo**: `identidad`
**Base URL**: `/api/auth`
**Sprint**: 1 — Checkpoint 1.3

---

## Endpoints

### POST /api/auth/login

Autentica un usuario con email y contraseña. Devuelve access token JWT y refresh token.

**Request:**
```json
{
  "email": "admin@municipio.gob.bo",
  "password": "contraseña-segura"
}
```

**Response 200:**
```json
{
  "accessToken": "<JWT>",
  "expiresAt": "2026-05-15T14:30:00Z",
  "refreshToken": "<token-opaco>",
  "usuario": {
    "id": "<guid>",
    "email": "admin@municipio.gob.bo",
    "nombreCompleto": "Administrador",
    "roles": ["Admin"]
  }
}
```

**Errores:**
| Código | Motivo auditado | Descripción |
|--------|-----------------|-------------|
| 401 | `usuario_inexistente` | Email no registrado |
| 401 | `password_incorrecto` | Contraseña inválida |
| 423 | `cuenta_bloqueada` | Cuenta bloqueada por intentos fallidos |

**Auditoría:** registra `accion: "login"` (OK) o `accion: "login_fallido"` (ERROR) en `auditoria.auditoria`.

---

### POST /api/auth/refresh

Rota el refresh token. El token anterior queda revocado con `revoked_reason: "rotacion"`.

**Request:**
```json
{
  "refreshToken": "<token-opaco>"
}
```

**Response 200:**
```json
{
  "accessToken": "<nuevo-JWT>",
  "expiresAt": "2026-05-15T14:45:00Z",
  "refreshToken": "<nuevo-token-opaco>"
}
```

**Errores:**
| Código | Descripción |
|--------|-------------|
| 401 | Token inexistente |
| 401 | Token expirado |
| 401 | Reutilización detectada (todos los tokens del usuario revocados) |

**Auditoría:** registra `accion: "refresh_token"` (OK) o `accion: "reutilizacion_detectada"` (ERROR).

---

### POST /api/auth/logout

Revoca el refresh token activo. Idempotente — siempre retorna 200.

**Request:**
```json
{
  "refreshToken": "<token-opaco>"
}
```

**Response 200:** (sin body)

**Auditoría:** registra `accion: "logout"` con `motivo: "token_inexistente"` si el token ya estaba revocado o no existe.

---

### GET /api/auth/me

Retorna información del usuario autenticado. Requiere `Authorization: Bearer <JWT>`.

**Response 200:**
```json
{
  "id": "<guid>",
  "email": "admin@municipio.gob.bo",
  "nombreCompleto": "Administrador",
  "roles": ["Admin"]
}
```

**Errores:**
| Código | Descripción |
|--------|-------------|
| 401 | Token ausente, expirado o inválido |

---

## Seguridad

- Access token: JWT HS256, 15 minutos de vigencia.
- Refresh token: 64 bytes aleatorios base64url, 7 días de vigencia.
- Detección de reutilización: si un token revocado se presenta de nuevo, todos los tokens activos del usuario se revocan (rotación completa).
- Lockout: 5 intentos fallidos bloquean la cuenta (configuración de ASP.NET Identity).
- Los valores del refresh token nunca aparecen en los logs de auditoría (ADR 0018).

---

## Convenciones de auditoría

Ver [ADR 0011](../decisiones/0011-convencion-nombres-modulo-auditoria.md) para la convención completa de valores `modulo`, `accion` y `motivo`.

| Campo | Valor |
|-------|-------|
| `modulo` | `"identidad"` |
| `accion` | snake_case: `login`, `login_fallido`, `logout`, `refresh_token`, `reutilizacion_detectada` |
| `motivo` | snake_case: `usuario_inexistente`, `cuenta_bloqueada`, `password_incorrecto`, `token_inexistente` |
