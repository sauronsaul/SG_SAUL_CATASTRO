# Sprint 1 — Checkpoint 1.1 Resumen

**Fecha**: 2026-05-11
**Checkpoint**: 1.1 de 4 del Sprint 1
**Responsable**: Saul Gutierrez + Claude Code

---

## Objetivos y estado

| # | Objetivo | Estado |
|---|---|---|
| 1 | `.gitattributes` con política de EOL por tipo de archivo | ✅ Completado |
| 2 | Solución `.NET 10` con `SG.slnx` | ✅ Completado |
| 3 | 5 proyectos de producción con estructura Clean Architecture | ✅ Completado |
| 4 | 3 proyectos de pruebas xUnit | ✅ Completado |
| 5 | Referencias entre proyectos respetando dependencias de capas | ✅ Completado |
| 6 | `Directory.Build.props` con configuración global | ✅ Completado |
| 7 | `Directory.Packages.props` — Central Package Management | ✅ Completado |
| 8 | `tests/Directory.Build.props` con import explícito al padre | ✅ Completado |
| 9 | Todos los paquetes NuGet del stack instalados y con versiones resueltas | ✅ Completado |
| 10 | `dotnet build` → 0 errores, 0 advertencias | ✅ Completado |
| 11 | `dotnet test` → 3/3 proyectos en verde | ✅ Completado |
| 12 | Corrección: `Serilog.AspNetCore` solo en `SG.Api`, no en `SG.Infrastructure` | ✅ Aplicado |
| 13 | ADR 0005 (CPM), ADR 0006 (warnings as errors), ADR 0007 (.slnx) | ✅ Completado |

---

## Decisiones técnicas

| Decisión | Justificación |
|---|---|
| Formato de solución `.slnx` | Default del SDK .NET 10; XML estándar más legible que `.sln` clásico. VS Code + C# Dev Kit lo soporta. (ADR 0007) |
| `TreatWarningsAsErrors=true` globalmente | Previene acumulación silenciosa de deuda técnica. Cero warnings = build verde. (ADR 0006) |
| `AnalysisLevel=latest-recommended` + `EnforceCodeStyleInBuild=true` | Analizadores Roslyn activos durante build: detectan problemas antes de runtime. |
| `CS1591` silenciado globalmente | `GenerateDocumentationFile=true` lo activa en todo código público; se exigirá documentación selectivamente al madurar el dominio. |
| `tests/Directory.Build.props` con `<Import>` explícito | MSBuild se detiene en el primer `Directory.Build.props` encontrado. Sin import explícito, los tests no heredarían `TargetFramework` y el build fallaría. |
| `Serilog.AspNetCore` solo en `SG.Api` | `Serilog.AspNetCore` depende de `Microsoft.AspNetCore.App`. Incluirlo en `SG.Infrastructure` (que usa `Microsoft.NET.Sdk`) introduciría dependencia de framework web en la capa de infraestructura, violando Clean Architecture. |
| Central Package Management (CPM) | Una sola fuente de verdad para versiones NuGet. Actualizar un paquete = editar una línea en `Directory.Packages.props`. (ADR 0005) |
| `Microsoft.AspNetCore.OpenApi` nativo en lugar de Swashbuckle | El SDK .NET 10 incluye generación de spec OpenAPI nativa en el template `webapi`. Swashbuckle queda disponible como UI adicional si se requiere en checkpoint posterior. |

---

## Versiones de paquetes resueltas

| Paquete | Versión | Capa |
|---|---|---|
| MediatR | 14.1.0 | Application |
| FluentValidation | 12.1.1 | Application |
| FluentValidation.DependencyInjectionExtensions | 12.1.1 | Application |
| Mapster | 10.0.7 | Application |
| Mapster.DependencyInjection | 10.0.7 | Application |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.7 | Application |
| NetTopologySuite | 2.6.0 | Contracts |
| Microsoft.EntityFrameworkCore | 10.0.7 | Infrastructure |
| Microsoft.EntityFrameworkCore.Design | 10.0.7 | Infrastructure |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.1 | Infrastructure |
| Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite | 10.0.1 | Infrastructure |
| EFCore.NamingConventions | 10.0.1 | Infrastructure |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 10.0.7 | Infrastructure |
| BCrypt.Net-Next | 4.2.0 | Infrastructure |
| Serilog | 4.3.1 | Infrastructure |
| Serilog.Sinks.Console | 6.1.1 | Infrastructure |
| Serilog.Sinks.File | 7.0.0 | Infrastructure |
| Serilog.Enrichers.Environment | 3.0.1 | Infrastructure |
| Serilog.Enrichers.Thread | 4.0.0 | Infrastructure |
| Microsoft.Extensions.Http | 10.0.7 | Infrastructure |
| Microsoft.AspNetCore.OpenApi | 10.0.7 | Api |
| Microsoft.AspNetCore.Authentication.JwtBearer | 10.0.7 | Api |
| Serilog.AspNetCore | 10.0.0 | Api |
| DotNetEnv | 3.2.0 | Api |
| xunit | 2.9.3 | Tests |
| xunit.runner.visualstudio | 3.1.4 | Tests |
| Microsoft.NET.Test.Sdk | 17.14.1 | Tests |
| coverlet.collector | 6.0.4 | Tests |
| FluentAssertions | 8.9.0 | Tests |
| NSubstitute | 5.3.0 | Application.Tests |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.7 | IntegrationTests |
| Testcontainers.PostgreSql | 4.11.0 | IntegrationTests |

---

## Pendientes para Checkpoint 1.2

- Implementar entidad `Usuario` en `SG.Domain` (sin dependencias externas).
- Implementar `IdentityUser` derivado con campos del dominio en `SG.Infrastructure`.
- Configurar `DbContext` base con EF Core 10 + Npgsql + snake_case.
- Configurar cadena de conexión desde `.env` vía `DotNetEnv`.
- Primera migración EF Core.
- Verificar que `dotnet ef migrations add Inicial` funciona contra PostgreSQL real
  levantado con docker-compose.

## Notas para Saul

- El build `Release` pasa en 3-4 segundos (caché de restauración caliente). La
  primera compilación tardó ~21 segundos descargando dependencias transitivas.
- `tests/Directory.Build.props` es un patrón estándar de .NET para proyectos
  de test — no es deuda técnica, es la forma correcta de heredar configuración
  en soluciones con subdirectorios.
- `Serilog.AspNetCore` en `SG.Infrastructure` fue detectado y corregido durante
  la revisión de Saul antes del commit — la corrección queda registrada en este
  resumen como decisión de arquitectura (no como incidente).
- Node.js v25 sigue pendiente de migrar a 22 LTS — no bloqueante hasta
  Checkpoint 1.4 / Sprint 4 (frontend).
