# Índice de ADRs — SG_SAUL_CATASTRO

## Convención de numeración

- Los números reflejan **orden cronológico de creación**, no secuencia obligatoria.
- **Los huecos son intencionales**: corresponden a ADRs planificados que se aplazaron
  por reorganización de checkpoints. Se documentarán en el checkpoint en que se
  implementen, con el siguiente número libre en ese momento.
- Un ADR aplazado **nunca recibe el número "reservado" original**; recibe el número
  siguiente disponible cuando se crea. Esto preserva la integridad del historial.

## Por qué hay huecos

| Rango ausente | Motivo |
|---|---|
| 0002 – 0004 | ADRs de auth JWT y configuración inicial aplazados al Checkpoint 1.3+ |
| 0008 – 0011 | ADRs de interceptor auditoría y schemas PostgreSQL aplazados al Checkpoint 1.3+ |

---

## Índice completo

| # | Título | Sprint / Checkpoint |
|---|---|---|
| [0001](0001-stack-tecnologico.md) | Stack tecnológico | Sprint 0 |
| [0005](0005-central-package-management.md) | Central Package Management con Directory.Packages.props | 1.1 |
| [0006](0006-treat-warnings-as-errors.md) | TreatWarningsAsErrors y política de supresión | 1.1 |
| [0007](0007-slnx-en-lugar-de-sln.md) | Usar formato .slnx en lugar de .sln clásico | 1.1 |
| [0012](0012-pruebas-destructivas-archivos-config.md) | Protocolo para pruebas que tocan archivos de configuración del operador | 1.2 |
| [0013](0013-snake-case-tablas-identity.md) | snake_case completo en tablas e índices de ASP.NET Identity | 1.2 |
| [0014](0014-connection-string-caracteres-prohibidos.md) | Caracteres permitidos en passwords de connection strings | 1.2 |
| [0015](0015-pg-hba-scram-sha-256.md) | pg_hba.conf: scram-sha-256 para todas las conexiones | 1.2 |
| [0016](0016-validacion-migraciones-via-red-no-socket.md) | Validación de migraciones: solo vía red, nunca vía socket | 1.2 |
| [0017](0017-conflicto-puerto-postgres-local.md) | Puerto 5434 para el contenedor PostgreSQL en desarrollo local (PG16=5432, PG17=5433) | 1.2 |
| [0018](0018-no-divulgacion-secretos.md) | Protocolo de no-divulgación de secretos | 1.2 |
| [0019](0019-include-error-detail-solo-en-desarrollo.md) | Include Error Detail habilitado solo en desarrollo | 1.2 |
