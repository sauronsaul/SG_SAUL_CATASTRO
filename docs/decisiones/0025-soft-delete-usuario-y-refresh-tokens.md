# ADR 0025 — Soft-delete de UsuarioIdentidad y revocación de RefreshTokens

**Estado**: Aceptado  
**Fecha**: 2026-05-14  
**Sprint**: 1 — Checkpoint 1.3  
**Autor**: Saul Gutierrez

---

## Contexto

EF Core emite el siguiente warning al arrancar la API:

```
Entity 'UsuarioIdentidad' has a global query filter defined and is the required end
of a relationship with the entity 'RefreshToken'. This may lead to unexpected results
when the required entity is filtered out.
```

**Causa técnica:**  
`UsuarioIdentidadConfiguration` aplica `builder.HasQueryFilter(x => !x.IsDeleted)` (soft-delete). `RefreshToken.Usuario` es un navegador requerido (non-nullable) con FK hacia `UsuarioIdentidad`. EF Core advierte que si el usuario está soft-deleted (filtrado por el query filter), el navegador requerido no cargará correctamente.

**Soluciones técnicas consideradas:**

1. **Filtro espejo en RefreshToken** — `HasQueryFilter(rt => !rt.Usuario.IsDeleted)` para que los tokens de usuarios soft-deleted también queden filtrados.
2. **Navegador opcional** — cambiar `UsuarioIdentidad Usuario` a nullable en la entidad `RefreshToken`.
3. **Revocación explícita** — cuando un usuario se elimina, revocar sus tokens en el handler antes de marcar `IsDeleted = true`.
4. **Aceptar el warning** como deuda controlada mientras no exista endpoint de eliminación de usuarios.

---

## Decisión

**Se acepta el warning en Checkpoint 1.3 como deuda controlada.** No se agrega filtro espejo.

Cuando se implemente `EliminarUsuarioCommand` (Sprint 2 o posterior), el handler DEBE revocar explícitamente todos los refresh tokens activos del usuario afectado dentro de la misma transacción, antes de aplicar `IsDeleted = true`.

---

## Justificación

1. **El warning es benigno en el estado actual.** Ningún código de los handlers accede a `RefreshToken.Usuario` como navegador — todos los lookups son directos por `TokenString` (vía `IRefreshTokenRepositorio.BuscarPorTokenAsync`) o por `UsuarioId`. El query filter de `UsuarioIdentidad` no afecta estas rutas de consulta.

2. **El filtro espejo esconde el problema sin resolverlo.** Si un usuario se marca `IsDeleted = true` sin revocar sus tokens, esos tokens siguen válidos en la BD. El query filter solo los ocultaría de las queries EF, pero cualquier código que acceda directamente a SQL aún los vería. La solución correcta es la revocación explícita.

3. **El problema real no existe todavía.** No hay endpoint de eliminación de usuarios en Sprint 1. Crear deuda controlada documentada es preferible a implementar una "solución" que da falsa seguridad.

4. **La revocación explícita es la arquitectura correcta.** Alinea con el patrón del `LogoutCommandHandler` (revocación activa) y es consistente con el modelo de auditoría (genera registro de `AccionAuditoria.Delete` con motivo explícito).

---

## Consecuencias

### Positivas
- Sin código adicional innecesario en Sprint 1.
- La solución correcta queda documentada y priorizada.
- No se introduce seguridad falsa (filtro que oculta sin revocar).

### Negativas / Riesgos controlados
- El warning persiste en los logs de arranque hasta Sprint 2.
- Si alguien implementa eliminación de usuarios sin consultar este ADR, los tokens podrían quedar activos. **Mitigación:** comentario TODO en `RefreshToken.cs` y `RefreshTokenConfiguration.cs`.

---

## Plan de acción (Sprint 2)

Cuando se cree `EliminarUsuarioCommand`:

```csharp
// Paso 1: Revocar todos los refresh tokens activos del usuario
await refreshTokens.RevocarTodosAsync(
    usuarioId, operadorIpOrigen, "UsuarioEliminado", cancellationToken);

// Paso 2: Soft-delete del usuario
usuario.IsDeleted = true;
await db.SaveChangesAsync(cancellationToken);

// Paso 3: Registro de auditoría del comando
```

---

---

## Nota: inmutabilidad de registros históricos

Los valores de `revoked_reason` y `accion` en registros de auditoría anteriores al fix de snake_case (commits `5fb1b98` y anteriores) permanecen en PascalCase (`ReutilizacionDetectada`, `Rotacion`, `Login`, `LoginFallido`, `RefreshToken`). La auditoría es inmutable por diseño: modificar registros antiguos violaría el principio fundamental de inmutabilidad y constituiría falsificación de evidencia. La inconsistencia histórica es información válida sobre la evolución del sistema.

---

## Nota: nomenclatura de revoked_reason

Los valores del campo `revoked_reason` en `identidad.refresh_tokens` son strings **snake_case**:
- `'rotacion'` — token consumido en un refresh exitoso y reemplazado por uno nuevo.
- `'reutilizacion_detectada'` — token ya revocado fue presentado de nuevo (ataque de reutilización).
- `'usuario_eliminado'` — (reservado Sprint 2) todos los tokens del usuario revocados al eliminarlo.

No usar `.ToString()` de `AccionAuditoria` u otro enum: produciría PascalCase (`Rotacion`, `ReutilizacionDetectada`), inconsistente con el `snake_case` del resto de la base de datos. Los valores se pasan como strings literales desde los handlers de la capa Application.

Esta misma convención aplica al campo `accion` de `auditoria.auditoria` cuando se registra un evento relacionado con refresh tokens.

---

## Referencias

- [RefreshToken.cs](../../src/backend/SG.Infrastructure/Identidad/RefreshToken.cs)
- [RefreshTokenConfiguration.cs](../../src/backend/SG.Infrastructure/Persistencia/Configurations/RefreshTokenConfiguration.cs)
- [UsuarioIdentidadConfiguration.cs](../../src/backend/SG.Infrastructure/Persistencia/Configurations/UsuarioIdentidadConfiguration.cs)
- [RefreshTokenCommandHandler.cs](../../src/backend/SG.Application/Autenticacion/Refresh/RefreshTokenCommandHandler.cs)
- ADR 0018 — Seguridad de secretos JWT
