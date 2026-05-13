# ADR 0013 — snake_case completo en tablas e índices de ASP.NET Identity

**Fecha**: 2026-05-12
**Estado**: Aceptado
**Autor**: Saul Gutierrez

---

## Contexto

ASP.NET Core Identity establece en `IdentityDbContext.OnModelCreating` nombres
explícitos (hardcodeados) para tablas e índices, usando sus propias convenciones
internas. Al configurar `UseSnakeCaseNamingConvention()` (EFCore.NamingConventions),
las **columnas** se convierten a snake_case automáticamente, pero los nombres
hardcodeados **no** — porque Identity los aplica con llamadas explícitas antes
de que nuestras configuraciones tengan efecto.

### Problema observado en el SQL de M001 (primera iteración)

**Tablas** (cinco de unión/metadatos):

```sql
CREATE TABLE identidad."AspNetRoleClaims" (...);
CREATE TABLE identidad."AspNetUserClaims" (...);
-- etc.
```

**Índices** (tres de búsqueda en las tablas principales):

```sql
CREATE UNIQUE INDEX "RoleNameIndex" ON identidad.roles (normalized_name);
CREATE UNIQUE INDEX "EmailIndex"    ON identidad.usuarios (normalized_email);
CREATE UNIQUE INDEX "UserNameIndex" ON identidad.usuarios (normalized_user_name);
```

Los identificadores entre comillas en PostgreSQL son case-sensitive y visualmente
inconsistentes con el resto del esquema.

## Decisión

**Convención única, sin excepciones por origen del paquete.** Se corrigen tanto
los nombres de tabla como los nombres de índice, ambos mediante
`IEntityTypeConfiguration<T>` explícitas aplicadas vía
`ApplyConfigurationsFromAssembly` después de `base.OnModelCreating`.

### Tablas (5 configuraciones nuevas)

| Tipo Identity | Tabla anterior | Tabla nueva |
|---|---|---|
| `IdentityUserClaim<Guid>` | `AspNetUserClaims` | `usuario_claims` |
| `IdentityUserLogin<Guid>` | `AspNetUserLogins` | `usuario_logins` |
| `IdentityUserToken<Guid>` | `AspNetUserTokens` | `usuario_tokens` |
| `IdentityRoleClaim<Guid>` | `AspNetRoleClaims` | `rol_claims` |
| `IdentityUserRole<Guid>` | `AspNetUserRoles` | `usuario_roles` |

### Índices (en configuraciones existentes de las entidades principales)

| Índice anterior | Índice nuevo | Configuración |
|---|---|---|
| `"RoleNameIndex"` | `ix_roles_normalized_name` | `RolIdentidadConfiguration` |
| `"EmailIndex"` | `ix_usuarios_normalized_email` | `UsuarioIdentidadConfiguration` |
| `"UserNameIndex"` | `ix_usuarios_normalized_user_name` | `UsuarioIdentidadConfiguration` |

El prefijo `ix_` es la convención PostgreSQL estándar para índices, y coincide con
el patrón que EFCore.NamingConventions genera automáticamente para todos los
índices NO hardcodeados por Identity.

## Justificación

Tener tablas e índices en snake_case junto a identificadores entre comillas en el
mismo schema obliga a cualquier consulta SQL directa, herramienta de introspección
o migración futura a conocer cuáles objetos son "de Identity" para tratarlos
diferente. Ese conocimiento contextual es deuda técnica silenciosa.

Corregirlo en el origen (primera migración, base de datos vacía) tiene costo cero.
Corregirlo después de tener datos en producción requiere:
1. `RenameTable`/`RenameIndex` por cada objeto afectado.
2. Actualización de queries, vistas y funciones que los referencien.
3. Coordinación con operaciones en producción.

## Aplicación

```
SG.Infrastructure/Persistencia/Configurations/
  IdentityUserClaimConfiguration.cs    → ToTable("usuario_claims")
  IdentityUserLoginConfiguration.cs    → ToTable("usuario_logins")
  IdentityUserTokenConfiguration.cs    → ToTable("usuario_tokens")
  IdentityRoleClaimConfiguration.cs    → ToTable("rol_claims")
  IdentityUserRoleConfiguration.cs     → ToTable("usuario_roles")
  RolIdentidadConfiguration.cs         → HasDatabaseName("ix_roles_normalized_name")
  UsuarioIdentidadConfiguration.cs     → HasDatabaseName("ix_usuarios_normalized_email")
                                       → HasDatabaseName("ix_usuarios_normalized_user_name")
```

Todas son detectadas automáticamente por `ApplyConfigurationsFromAssembly`.

## Consecuencias

**Positivas**:
- Esquema PostgreSQL 100% snake_case. Cero identificadores entre comillas.
- Índices nombrados consistentemente con prefijo `ix_` en todo el schema.
- Queries directas, `\di` en psql y herramientas de introspección funcionan
  de forma predecible sin conocimiento especial del origen de los objetos.
- Sin deuda técnica desde el primer día.

**Negativas / compromisos**:
- Si en el futuro se actualiza `Microsoft.AspNetCore.Identity.EntityFrameworkCore`
  y agrega nuevas tablas o índices hardcodeados, éstos volverán a usar nombres
  no estándar hasta que se agregue la configuración correspondiente. Requiere
  vigilancia en actualizaciones del paquete.
