# Sprint 0 — Resumen

**Fecha**: 2026-05-11
**Duración**: 1 sesión (días 1-2 del roadmap)
**Responsable**: Saul Gutierrez + Claude Code

---

## Objetivos y estado

| # | Objetivo | Estado |
|---|---|---|
| 1 | Inicializar repositorio Git local + remoto SSH | ✅ Completado |
| 2 | Crear rama `main` y rama `develop` | ✅ Completado |
| 3 | Estructura completa de carpetas (sección 5 CLAUDE.md) | ✅ Completado |
| 4 | `.gitignore` exhaustivo (.NET, Node, IDEs, Docker, OS, secrets) | ✅ Completado |
| 5 | `.editorconfig` (C# 4sp, TS/YAML/JS 2sp, LF para sh/yml) | ✅ Completado |
| 6 | `.env.example` documentado con todas las variables agrupadas | ✅ Completado |
| 7 | `README.md` raíz con secciones, badges y árbol | ✅ Completado |
| 8 | `docker-compose.yml` con postgres, minio y caddy | ✅ Completado |
| 9 | `docker-compose.local.yml` y `docker-compose.prod.yml` (placeholder) | ✅ Completado |
| 10 | `Caddyfile.local` con reverse proxy placeholder api/web | ✅ Completado |
| 11 | `api.Dockerfile` y `web.Dockerfile` (placeholder Sprint 1/4) | ✅ Completado |
| 12 | `database/init/01-extensions.sql` (postgis, pg_trgm, unaccent, uuid-ossp) | ✅ Completado |
| 13 | `scripts/start-local.sh` y `stop-local.sh` | ✅ Completado |
| 14 | `.github/workflows/ci.yml` (lint-yaml, docker-compose-validate, placeholder-test) | ✅ Completado |
| 15 | Docs: visión general arquitectura, ADR 0001 stack, instalación placeholder | ✅ Completado |
| 16 | Commit inicial + push a main y develop | ✅ Completado |

---

## Decisiones tomadas

| Decisión | Justificación |
|---|---|
| `postgis/postgis:16-3.4-alpine` como imagen PostgreSQL | Alpine reduce tamaño; PostGIS 3.4 es la versión estable compatible con EPSG:32719 y las extensiones requeridas. |
| Caddy 2 como reverse proxy desde Sprint 0 | Simplifica el modelo mental: un solo punto de entrada desde el principio. TLS automático en Sprint 7. |
| `healthcheck` en postgres y minio con `depends_on` | Caddy no inicia hasta que los servicios base estén listos; evita errores de arranque en frío. |
| Scripts `.sh` con LF | Git Bash en Windows requiere LF para ejecutar sin errores de `\r`. |
| `docker-compose.local.yml` y `docker-compose.prod.yml` vacíos (`services: {}`) | Evita errores de compose al combinar archivos; placeholder listo para Sprint 1. |
| Commit único en Sprint 0 | El sprint es de estructura pura, sin lógica de negocio; un solo commit documenta el estado de partida limpiamente. |

---

## Resultado de validación Docker

```
# docker compose config → sin warnings ni errores
# docker compose up -d  → todas las imágenes descargadas correctamente

NAME          IMAGE                           STATUS
sg_caddy      caddy:2-alpine                  Up (running)
sg_minio      minio/minio:latest              Up (healthy)
sg_postgres   postgis/postgis:16-3.4-alpine   Up (healthy)

# docker compose down → todos los contenedores detenidos, volúmenes conservados
```

---

## Pendientes para Sprint 1 (días 3-5)

- Crear solución .NET 10: `SG.sln` con proyectos `SG.Api`, `SG.Application`, `SG.Domain`, `SG.Infrastructure`, `SG.Contracts`.
- Implementar autenticación JWT + Refresh Token.
- Implementar primera entidad del dominio: `Usuario` con Identity.
- Configurar EF Core con PostgreSQL/PostGIS + migraciones.
- Exponer endpoint de login funcional verificable en Swagger.
- Cobertura de tests de dominio ≥ 80%.
- Implementar `api.Dockerfile` multi-stage real.

---

## Notas técnicas

- Node.js v25.9.0 en entorno (pendiente migrar a 22 LTS — no bloqueante hasta Sprint 4).
- El `.env` real nunca se versiona; el `.env.example` es la referencia completa.
- Las advertencias de variables en `docker compose ps` (sin `--env-file`) son esperadas y no representan error.
