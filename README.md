# SG_SAUL_CATASTRO

![CI](https://github.com/sauronsaul/SG_SAUL_CATASTRO/actions/workflows/ci.yml/badge.svg)
![License](https://img.shields.io/badge/license-Propietario-red)
![Version](https://img.shields.io/badge/version-0.1.0--sprint0-blue)

Sistema institucional de gestión catastral municipal para el municipio de Caranavi, La Paz, Bolivia. Administra predios, propietarios, geometrías, trámites, valuación, certificados y auditoría completa, con soporte para despliegue local (una sola PC con Docker Desktop) y en servidor.

---

## Prerrequisitos

| Componente | Versión mínima | Notas |
|---|---|---|
| Docker Desktop | 29.4+ | Con Docker Compose integrado |
| Git | 2.54+ | Configurado con SSH para GitHub |
| .NET SDK | 10.0+ | Solo necesario para desarrollo del backend |
| Node.js | 22 LTS+ | Solo necesario para desarrollo del frontend |

---

## Inicio rápido (modo local)

```bash
# 1. Clonar el repositorio
git clone git@github.com:sauronsaul/SG_SAUL_CATASTRO.git
cd SG_SAUL_CATASTRO

# 2. Configurar variables de entorno
cp .env.example .env
# Editar .env con un editor de texto y cambiar los valores marcados con <CAMBIAR>

# 3. Levantar la infraestructura base
bash scripts/start-local.sh
```

Al terminar, el script mostrará las URLs de acceso a cada servicio.

Para detener:

```bash
bash scripts/stop-local.sh
```

---

## Estructura del repositorio

```
SG_SAUL_CATASTRO/
├── src/
│   ├── backend/          # .NET 10 — Api, Application, Domain, Infrastructure, Contracts
│   └── frontend/sg-web/  # Vite + React + TypeScript + Ant Design 5
├── database/
│   ├── init/             # Scripts SQL ejecutados al primer arranque del contenedor
│   └── seed/             # Datos semilla (catálogos, usuario admin)
├── infra/
│   ├── docker/           # docker-compose.yml + variantes local/prod + Dockerfiles
│   ├── backup/           # Scripts de backup pg_dump
│   └── tiles/            # Configuración de pg_tileserv y tileserver-gl
├── data-migration/       # Shapefiles, planillas y datos legados para migración
├── docs/                 # Arquitectura, dominio, API, GIS, operación, decisiones, normativa
├── scripts/              # start-local.sh, stop-local.sh
└── .github/workflows/    # CI/CD con GitHub Actions
```

---

## Cómo correr tests

> Pendiente — Sprint 1+

```bash
# Backend (xUnit)
dotnet test src/backend/SG.sln

# Frontend (Vitest)
cd src/frontend/sg-web && npm run test
```

---

## Documentación

- [Visión general de arquitectura](docs/arquitectura/01-vision-general.md)
- [ADR 0001 — Stack tecnológico](docs/decisiones/0001-stack-tecnologico.md)
- [Instalación modo local](docs/operacion/instalacion-modo-local.md)
- [Contexto maestro del proyecto (CLAUDE.md)](CLAUDE.md)

---

## Licencia

Propietario — Municipio de Caranavi, La Paz, Bolivia. Todos los derechos reservados.
