# Visión General de Arquitectura

SG_SAUL_CATASTRO es un sistema institucional de gestión catastral municipal que sigue los principios de **Clean Architecture**, separando estrictamente el dominio de la infraestructura y la presentación. El backend es .NET 10 con ASP.NET Core, EF Core y CQRS vía MediatR; el frontend es React 18 con Ant Design 5; la base de datos es PostgreSQL 16 con PostGIS 3.4; el almacenamiento de documentos usa MinIO; y todo corre sobre Docker Compose.

Para el contexto completo del sistema, la arquitectura por capas, las reglas de negocio y el stack tecnológico oficial, ver [CLAUDE.md](../../CLAUDE.md).

## Capas

```
Presentación (React, QGIS, QField)
       ↓ HTTP REST/JSON
SG.Api (Controllers, Middleware)
       ↓
SG.Application (MediatR handlers, DTOs, Validators)
       ↓
SG.Domain (Entidades, Value Objects, Reglas) ← núcleo, sin dependencias
       ↑
SG.Infrastructure (EF Core, MinIO, Auth, Logs)
       ↓
PostgreSQL 16 + PostGIS 3.4
```

## SRID

Toda geometría usa **EPSG:32719** (UTM WGS84 zona 19 Sur — Bolivia occidental).
