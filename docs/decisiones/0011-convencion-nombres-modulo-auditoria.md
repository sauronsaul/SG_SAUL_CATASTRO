# ADR 0011 — Convención de nombres de módulo en auditoría

**Estado**: Aceptado  
**Fecha**: 2026-05-15  
**Sprint**: 1 — Checkpoint 1.3  
**Autor**: Saul Gutierrez

---

## Contexto

La tabla `auditoria.auditoria` tiene un campo `modulo varchar` que agrupa eventos por área funcional del sistema. Se detectó ambigüedad sobre qué valor usar: nombres descriptivos en PascalCase (`"Autenticacion"`, `"Predios"`) versus nombres de schema PostgreSQL en minúsculas (`"identidad"`, `"dominio"`).

La confusión surgió durante el Checkpoint 1.3, cuando la Prueba 9 (E2E de auditoría) filtró por `modulo='Autenticacion'` y retornó 0 filas, aunque la tabla tenía 94 registros bajo `modulo='identidad'`.

---

## Decisión

**El valor del campo `modulo` en `auditoria.auditoria` es el nombre del schema PostgreSQL correspondiente, en minúsculas.**

| Schema PostgreSQL | Valor de `modulo` |
|---|---|
| `identidad` | `"identidad"` |
| `dominio` | `"dominio"` |
| `auditoria` | `"auditoria"` |
| `cartografia` (futuro) | `"cartografia"` |

Esta convención aplica a todos los módulos presentes y futuros.

---

## Convención de valores de `accion`

Los valores del campo `accion` usan **snake_case**, sin excepción:

| Evento | Valor de `accion` |
|---|---|
| Login exitoso | `"login"` |
| Login fallido (cualquier causa) | `"login_fallido"` |
| Logout | `"logout"` |
| Rotación de refresh token | `"refresh_token"` |
| Reutilización de token detectada | `"reutilizacion_detectada"` |
| Inserción automática (interceptor) | `"Insert"` ← generado por EF Core, no modificable |
| Actualización automática (interceptor) | `"Update"` ← generado por EF Core, no modificable |

Los valores generados por el `AuditoriaInterceptor` de EF Core (`"Insert"`, `"Update"`, `"Delete"`) quedan en PascalCase porque provienen directamente del enum `AccionAuditoria.ToString()` interno del interceptor. No se normalizan para evitar modificar código de infraestructura en Sprint 1.

## Convención de valores de `motivo`

El campo `motivo` (opcional) también usa **snake_case** cuando se especifica:

| Contexto | Valor de `motivo` |
|---|---|
| Login fallido: usuario no existe | `"usuario_inexistente"` |
| Login fallido: cuenta bloqueada | `"cuenta_bloqueada"` |
| Token revocado: rotación | campo `revoked_reason`: `"rotacion"` |
| Token revocado: reutilización | campo `revoked_reason`: `"reutilizacion_detectada"` |
| Token revocado: usuario eliminado | campo `revoked_reason`: `"usuario_eliminado"` (Sprint 2) |

---

## Justificación

1. **Alineación con el modelo de datos**: los schemas de PostgreSQL son la unidad de organización oficial. Usar sus nombres directamente elimina una capa de traducción y evita ambigüedad.

2. **Consistencia con snake_case de la BD**: toda la nomenclatura de columnas, tablas y valores de sistema usa snake_case. Los strings de auditoría no son excepción.

3. **Facilita queries directas**: `WHERE modulo='identidad'` es más predecible que recordar si es `"Auth"`, `"Autenticacion"` o `"Authentication"`.

---

## Consecuencias

### Positivas
- Nomenclatura inequívoca y derivable mecánicamente del schema.
- Consistencia total en snake_case para todos los valores de sistema.

### Negativas
- Los registros históricos anteriores al Checkpoint 1.3 tienen `accion` en PascalCase (`"Login"`, `"LoginFallido"`, `"RefreshToken"`). Ver ADR 0025 para la política de inmutabilidad de registros históricos.
- Los valores del `AuditoriaInterceptor` (`"Insert"`, `"Update"`) permanecen en PascalCase por limitación del interceptor de EF Core.

---

## Referencias

- [AuditoriaService.cs](../../src/backend/SG.Infrastructure/Seguridad/AuditoriaService.cs)
- [AuditoriaInterceptor.cs](../../src/backend/SG.Infrastructure/Persistencia/Interceptors/AuditoriaInterceptor.cs)
- [LoginCommandHandler.cs](../../src/backend/SG.Application/Autenticacion/Login/LoginCommandHandler.cs)
- ADR 0025 — Soft-delete y revocación de RefreshTokens (política de inmutabilidad histórica)
