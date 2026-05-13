# Sprint 1 — Checkpoint 1.2 Resumen

**Fecha**: 2026-05-12
**Checkpoint**: 1.2 de 4 del Sprint 1
**Responsable**: Saul Gutierrez + Claude Code

---

## Objetivos y estado

| # | Objetivo | Estado |
|---|---|---|
| 1 | Entidad `Usuario` en `SG.Domain` (sin dependencias externas) | ✅ Completado |
| 2 | `IdentityUser` derivado (`UsuarioIdentidad`) con campos del dominio | ✅ Completado |
| 3 | `RolIdentidad` derivado de `IdentityRole` | ✅ Completado |
| 4 | `RefreshToken` como entidad de infraestructura | ✅ Completado |
| 5 | `AggregateRoot`, `ValueObject`, `Result<T>`, `DomainError` en `SG.Domain.Common` | ✅ Completado |
| 6 | `ApplicationDbContext` con EF Core 10 + Npgsql + NetTopologySuite + snake_case | ✅ Completado |
| 7 | Esquema `identidad` en PostgreSQL para todas las tablas de Identity | ✅ Completado |
| 8 | 5 configuraciones explícitas para tablas de unión de Identity (snake_case) | ✅ Completado |
| 9 | Índices de Identity renombrados a snake_case con prefijo `ix_` | ✅ Completado |
| 10 | `AuditableEntitiesInterceptor` (rellena `created_at`, `updated_at`, `created_by`, `updated_by`) | ✅ Completado |
| 11 | `AuditoriaInterceptor` (registra automáticamente en tabla `auditoria`) | ✅ Completado |
| 12 | `ApplicationDbContextFactory` con carga de `.env` y error educativo | ✅ Completado |
| 13 | `ICurrentUserService` + `CurrentUserService` en `SG.Application.Abstractions` / `SG.Infrastructure` | ✅ Completado |
| 14 | Cadena de conexión desde `.env` vía `DotNetEnv` (búsqueda ascendente de carpetas) | ✅ Completado |
| 15 | `Include Error Detail` controlado por `appsettings.Development.json` (no en `.env`) | ✅ Completado |
| 16 | Primera migración `M001_Inicial_Identidad_Auditoria` generada y aplicada | ✅ Completado |
| 17 | `pg_hba.conf` explícito con `scram-sha-256` para todas las conexiones | ✅ Completado |
| 18 | Puerto 5434 en `docker-compose.yml` y `.env.example` | ✅ Completado |
| 19 | Health check `/health` con `AddDbContextCheck<ApplicationDbContext>` | ✅ Completado |
| 20 | `dotnet build -c Release` → 0 errores, 0 advertencias | ✅ Verificado |
| 21 | ADRs 0012–0019 documentados | ✅ Completado |
| 22 | Login JWT funcional (endpoint `/auth/login` + tokens) | ❌ Pendiente para 1.3 |

---

## Decisiones técnicas

| # | Decisión | Justificación | ADR |
|---|---|---|---|
| 1 | `AggregateRoot` con `uint RowVersion` (concurrencia optimista) | Evita conflictos silenciosos en actualizaciones concurrentes desde el primer día | — |
| 2 | `Result<T>` como envelope de retorno en dominio | Evita excepciones de flujo de control en la capa de dominio; los errores son valores, no surpresas | — |
| 3 | `DomainError` + `UsuarioErrores` como constantes estáticas | Mensajes de error del dominio centralizados, sin strings dispersos, sin magic strings | — |
| 4 | `ICurrentUserService` en `SG.Application.Abstractions` | El dominio y la aplicación no pueden depender de `IHttpContextAccessor` (capa de presentación). La abstracción invierte la dependencia correctamente | — |
| 5 | Esquema `identidad` separado del esquema `public` en PostgreSQL | Aísla las tablas de ASP.NET Identity del dominio catastral. Facilita permisos PostgreSQL granulares y backups selectivos | — |
| 6 | snake_case completo en tablas e índices de Identity (5 configs + 3 índices) | ASP.NET Identity hardcodea nombres con PascalCase. `UseSnakeCaseNamingConvention()` no los alcanza. Corrección en primera migración = costo cero. Después de datos en producción: `RenameTable` + `RenameIndex` + coordinación | ADR 0013 |
| 7 | Caracteres permitidos en passwords: `[A-Za-z0-9_.-]` únicamente | Npgsql parsea connection strings usando `;` como separador. Un password con `;` se trunca silenciosamente → autenticación falla con `28P01`. Detectado en producción sería un incidente de seguridad. Restricción documentada en `.env.example` | ADR 0014 |
| 8 | `pg_hba.conf` explícito con `scram-sha-256` para socket Unix y TCP | La imagen `postgis/postgis:16-3.4` usa `trust` para conexiones locales por defecto. Cualquier proceso dentro del contenedor tiene acceso sin password. El endurecimiento garantiza que el único camino válido es TCP + scram-sha-256 | ADR 0015 |
| 9 | Migraciones validadas SOLO vía `dotnet ef database update`, nunca vía `psql` en contenedor | `psql` con `trust` omite autenticación real, omite `IDesignTimeDbContextFactory`, da falsos positivos. El error de `dotnet ef` es información — hay que resolverlo, no rodearlo | ADR 0016 |
| 10 | Puerto 5434 para el contenedor PostgreSQL | PG16 ocupa 5432 (`0.0.0.0`), PG17 ocupa 5433 (`0.0.0.0`) en la máquina de desarrollo. Docker mapea en `:::PPPP` (IPv6); el nativo escucha en `0.0.0.0:PPPP` (IPv4). Npgsql conecta a IPv4 → llega al nativo, no al contenedor | ADR 0017 |
| 11 | `Include Error Detail=true` en `appsettings.Development.json`, no en `.env` | Variables de entorno tienen mayor prioridad que `appsettings.Production.json` en ASP.NET Core 10. Si `.env` se carga en un servidor, `Include Error Detail` llegaría a producción | ADR 0019 |
| 12 | Protocolo backup/restore para tests que tocan archivos del operador | `Remove-Item .env` seguido de test es destructivo e irrecuperable. Además: en el caso concreto el test arrojó resultado correcto por razones incorrectas (binarios viejos con fallback hardcoded) | ADR 0012 |
| 13 | `Microsoft.AspNetCore.Http.Abstractions` en `SG.Infrastructure.csproj` | `IHttpContextAccessor` no está disponible en `Microsoft.NET.Sdk`. Sin este paquete, `CurrentUserService` no compila. No viola Clean Architecture: `SG.Infrastructure` usa la abstracción de HTTP, no la capa de presentación | — |

---

## Incidentes y aprendizajes

### Incidente 1 — Falso positivo por `trust` en pg_hba.conf

**Qué ocurrió**: La migración M001 fue aplicada con `psql` desde dentro del contenedor (sin password, usando socket Unix). Se asumió que el flujo estaba validado. Al intentar `dotnet ef database update`, falló con `28P01` — por un motivo completamente distinto (ver Incidente 2). La BD estaba en el estado correcto por una vía que nunca usaría la aplicación real.

**Aprendizaje**: Un test que pasa por razones incorrectas es más peligroso que un test que falla. El workaround con `psql` enmascaró el problema real durante horas.

**Corrección**: `pg_hba.conf` explícito con `scram-sha-256` hace imposible el workaround. ADR 0015 + ADR 0016.

---

### Incidente 2 — Password con `;` en connection string → autenticación silenciosa truncada

**Qué ocurrió**: `dotnet ef database update` fallaba con `28P01` (autenticación incorrecta) aunque el password en el contenedor era el correcto. El diagnóstico tardó porque el error de Npgsql no indicaba qué parte del password llegaba a PostgreSQL.

**Causa**: El password contenía `;`. Npgsql usa `;` para separar pares `clave=valor` en la connection string. Todo lo que sigue al primer `;` en el password se interpreta como claves adicionales. PostgreSQL recibe solo el fragmento anterior al `;`.

**Aprendizaje**: Los parsers de connection strings no son neutrales respecto a caracteres especiales. Ni Npgsql ni PostgreSQL producen un error descriptivo que señale la causa raíz. La restricción de caracteres debe documentarse explícitamente y comunicarse al operador antes del primer deploy.

**Corrección**: ADR 0014 + `.env.example` actualizado con conjunto de caracteres permitidos y comando de generación.

---

### Incidente 3 — Conflicto de puertos: 5432 (PG16) → 5433 (PG17) → 5434 (libre)

**Qué ocurrió**: El primer intento usó el puerto 5432 (conflicto con `postgresql-x64-16`). Se cambió a 5433, que también estaba ocupado por `postgresql-x64-17` — instalado en la misma máquina en el puerto por defecto. Npgsql conectaba a IPv4 (`127.0.0.1`), que llegaba al proceso nativo, no al contenedor Docker (que escucha en IPv6 `:::`).

**Aprendizaje**: Docker Desktop en Windows con instalaciones nativas de PostgreSQL crea un conflicto estructural: los contenedores exponen en IPv6, los procesos nativos escuchan en IPv4, y `localhost` puede resolver a cualquiera de los dos. El diagnóstico requiere `Get-NetTCPConnection` para ver qué proceso ocupa cada puerto.

**Corrección**: Puerto 5434 (libre en la máquina). ADR 0017.

---

### Incidente 4 — Secretos expuestos en pantalla compartida

**Qué ocurrió**: Durante una sesión de trabajo, el archivo `.env` con passwords reales quedó visible en pantalla compartida. Los passwords fueron regenerados de inmediato. Los anteriores nunca fueron usados en ningún sistema real, sin impacto operativo.

**Aprendizaje**: La exposición accidental de secretos es un riesgo operativo, no solo de seguridad. El protocolo de respuesta debe ser inmediato y no negociable. La regeneración es el único procedimiento correcto.

**Corrección**: ADR 0018 — 6 reglas permanentes de no-divulgación, incluyendo protocolo de pantalla compartida, verificación sin exposición (longitudes/hashes) y regeneración inmediata.

---

### Incidente 5 — Test destructivo sobre `.env` con falso positivo estructural

**Qué ocurrió**: Se intentó verificar que `ApplicationDbContextFactory` fallaba educativamente cuando `.env` no existía, usando `Remove-Item .env` seguido del test. El `Remove-Item` falló silenciosamente (`.env` no existía en ese momento). El test de "fallo educativo" también falló, pero no por la razón esperada: los binarios en caché tenían el fallback hardcodeado del ciclo anterior. El resultado parecía correcto pero por motivos incorrectos.

**Aprendizaje**: Un test que elimina un archivo del operador sin backup es tanto destructivo como epistemológicamente sospechoso. Si el precondición del test (que el archivo existe) no se verifica explícitamente, el resultado del test no tiene significado.

**Corrección**: ADR 0012 — protocolo backup/restore (`Rename-Item` + `-ErrorAction Stop` + verificación post-restauración).

---

## Estado del esquema de base de datos (M001)

La migración `M001_Inicial_Identidad_Auditoria` crea en el schema `identidad`:

| Tabla | Origen |
|---|---|
| `identidad.usuarios` | `UsuarioIdentidad : IdentityUser<Guid>` |
| `identidad.roles` | `RolIdentidad : IdentityRole<Guid>` |
| `identidad.usuario_roles` | `IdentityUserRole<Guid>` |
| `identidad.usuario_claims` | `IdentityUserClaim<Guid>` |
| `identidad.usuario_logins` | `IdentityUserLogin<Guid>` |
| `identidad.usuario_tokens` | `IdentityUserToken<Guid>` |
| `identidad.rol_claims` | `IdentityRoleClaim<Guid>` |
| `identidad.refresh_tokens` | `RefreshToken` |
| `identidad.auditoria` | `AuditoriaEntidad` |
| `identidad.__ef_migrations_history` | Historial EF Core |

Todos los identificadores son snake_case. Cero nombres entre comillas dobles.

---

## Pendientes para Checkpoint 1.3

| # | Tarea |
|---|---|
| 1 | Implementar `AuthService` con `LoginAsync`, `RefreshAsync`, `RevokeAsync` |
| 2 | Configurar JWT Bearer en `Program.cs` (generación y validación de tokens) |
| 3 | Endpoint `POST /api/auth/login` → retorna `access_token` + `refresh_token` |
| 4 | Endpoint `POST /api/auth/refresh` → rota el refresh token |
| 5 | Endpoint `POST /api/auth/logout` → revoca el refresh token |
| 6 | Script de seed para usuario `admin` en primer arranque (password desde `.env`) |
| 7 | Middleware de extracción de claims → `ICurrentUserService` retorna el usuario autenticado |
| 8 | Test de integración: login → acceso a endpoint protegido → refresh → logout |
| 9 | Verificar que `dotnet test` pasa con los tests de integración contra Testcontainers |

---

## Notas para Saul

- **El tiempo real vs estimado**: Checkpoint 1.2 tomó significativamente más de lo planeado por los cinco incidentes encadenados. Sin embargo, cada incidente produjo un ADR accionable. El sistema resultante es más robusto que si hubieran pasado sin fricción.

- **Los ADRs 0012–0019 son activos permanentes**: No son documentación de historia — son protocolos operativos que se aplican a partir de ahora. Especialmente el ADR 0014 (caracteres en passwords) debe recordarse en cada onboarding de un nuevo operador.

- **La migración M001 está aplicada y verificada** via `dotnet ef database update` con TCP + scram-sha-256 (flujo real de la aplicación). La tabla `__ef_migrations_history` tiene la entrada `M001_Inicial_Identidad_Auditoria`.

- **El build Release tarda ~15 segundos en caché caliente**. La primera compilación después de restaurar paquetes puede tardar más. No es un síntoma de problema.

- **`pg_hba.conf` endurecido implica** que los scripts de diagnóstico (DBeaver, pgAdmin, `docker exec psql`) necesitan proveer password explícitamente. Documentar en el gestor de contraseñas antes de olvidarlo.

- **El objetivo del Sprint 1 (Login JWT funcional) sigue siendo alcanzable** en Checkpoint 1.3. Los cimientos de autenticación (Identity, tokens, interceptores, DbContext) están completos y funcionando.
