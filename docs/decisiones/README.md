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
| 0008 – 0010 | ADRs de interceptor auditoría y schemas PostgreSQL aplazados al Checkpoint 1.3+ |
| 0020 – 0024 | ADRs de dominio catastral aplazados al Sprint 2 |

---

## Índice completo

| # | Título | Sprint / Checkpoint |
|---|---|---|
| [0001](0001-stack-tecnologico.md) | Stack tecnológico | Sprint 0 |
| [0005](0005-central-package-management.md) | Central Package Management con Directory.Packages.props | 1.1 |
| [0006](0006-treat-warnings-as-errors.md) | TreatWarningsAsErrors y política de supresión | 1.1 |
| [0007](0007-slnx-en-lugar-de-sln.md) | Usar formato .slnx en lugar de .sln clásico | 1.1 |
| [0011](0011-convencion-nombres-modulo-auditoria.md) | Convención de nombres de módulo en auditoría | 1.3 |
| [0012](0012-pruebas-destructivas-archivos-config.md) | Protocolo para pruebas que tocan archivos de configuración del operador | 1.2 |
| [0013](0013-snake-case-tablas-identity.md) | snake_case completo en tablas e índices de ASP.NET Identity | 1.2 |
| [0014](0014-connection-string-caracteres-prohibidos.md) | Caracteres permitidos en passwords de connection strings | 1.2 |
| [0015](0015-pg-hba-scram-sha-256.md) | pg_hba.conf: scram-sha-256 para todas las conexiones | 1.2 |
| [0016](0016-validacion-migraciones-via-red-no-socket.md) | Validación de migraciones: solo vía red, nunca vía socket | 1.2 |
| [0017](0017-conflicto-puerto-postgres-local.md) | Puerto 5434 para el contenedor PostgreSQL en desarrollo local (PG16=5432, PG17=5433) | 1.2 |
| [0018](0018-no-divulgacion-secretos.md) | Protocolo de no-divulgación de secretos | 1.2 |
| [0019](0019-include-error-detail-solo-en-desarrollo.md) | Include Error Detail habilitado solo en desarrollo | 1.2 |
| [0025](0025-soft-delete-usuario-y-refresh-tokens.md) | Soft-delete de UsuarioIdentidad y revocación de RefreshTokens | 1.3 |
| [0026](0026-mediatr-version-licencia.md) | MediatR 12.x (MIT) en lugar de MediatR 14.x (licencia comercial) | 1.3 |
| [0027](0027-cobertura-minima-con-criterios-exclusion.md) | Cobertura mínima con criterios de exclusión | 1.4 |
| [0028](0028-sincronizacion-jwt-secret-en-tests.md) | Sincronización de JWT Secret en Tests de Integración | 1.4 |
| [0029](0029-migraciones-auto-arranque-api.md) | Migraciones automáticas al arranque de la API | 1.4 |
| [0030](0030-dominio-catastral-diseno-sprint2.md) | Diseño del Dominio Catastral (Sprint 2 Checkpoint 2.1) | 2.1 |
| [0031](0031-deuda-tecnica-tipos-derecho-pendientes.md) | Deuda técnica: tipos de derecho pendientes en TipoDerecho | 2.1 |
| [0032](0032-catalogo-uso-suelo-convension-tablas.md) | Convención `catalogo_<tipo>` para tablas de catálogo | 2.1 |
| [0033](0033-ef-core-insert-vs-update-value-generated-never.md) | EF Core: INSERT vs UPDATE en entidades hijas — `ValueGeneratedNever()` | 2.2 |
| [0034](0034-testcontainers-connection-string-leak-y-sqlquery-bool.md) | Checkpoint 2.3: dos bugs de infraestructura de tests | 2.3 |
| [0035](0035-deuda-tecnica-limpieza-previews-huerfanos-minio.md) | Deuda técnica: limpieza de previews huérfanos en MinIO | Sprint 3 |
| [0036](0036-estrategia-transaccional-confirmacion-importacion.md) | Estrategia transaccional en ConfirmarImportacionHandler | Sprint 3 |
| [0037](0037-semantica-conteos-importacion.md) | Semántica de los campos de conteos del agregado Importacion | Sprint 3 |
| [0038](0038-auditoria-correcta-de-ownsone.md) | Auditoría correcta de entidades OwnsOne | Sprint 3 |
| [0039](0039-rotacion-obligatoria-secretos-filtrados.md) | Rotación obligatoria de secretos tras filtración detectada | Sprint 3 |
| [0040](0040-degradacion-fluentassertions-licencia.md) | Degradación de FluentAssertions de 8.9.0 a 7.2.2 | Sprint 3 |
| [0041](0041-auditoria-append-only-independiente.md) | Auditoría append-only e independiente del dominio | Sprint 3 |
| [0042](0042-deteccion-secretos-gitleaks.md) | Detección de secretos: gitleaks + wrapper de commit + hook pre-push | Sprint 4 |
| [0043](0043-timeouts-proxy-entorno-canonico.md) | Timeouts de proxy y entorno local canónico | Sprint 4 |
| [0044](0044-enforcement-append-only-auditoria.md) | Enforcement del invariante append-only en la tabla de auditoría | Sprint 4 |
| [0045](0045-modelo-valuacion-requisitos-datos.md) | Modelo de valuación catastral y requisitos de datos (piloto Uyuni) | Fase 0 |
| [0046](0046-stack-frontend-blazor-wasm.md) | Stack de frontend: Blazor WebAssembly | Fase 0 |
| [0047](0047-modelo-despliegue-nube-primero.md) | Modelo de despliegue: nube-primero | Fase 0 |
| [0048](0048-modelo-hibrido-desarrollo-nativo-contenedor-paridad.md) | Modelo híbrido: nativo para iteración y contenedor para paridad | Fase 0 |
| [0049](0049-importacion-reemplazo-completo-versionado-siete-capas-shp-datasetversion.md) | Importación SHP por reemplazo completo versionado | Fase 0 |
| [0050](0050-carga-versionada-asincrona-recuperable.md) | Carga versionada asíncrona y recuperación tras reinicio | Fase 0 |
| [0051](0051-preview-activacion-reconciliacion-dataset.md) | Preview, activación atómica y reconciliación del maestro | Fase 1 |
| [0052](0052-tolerancia-geometrias-reales-capas-versionadas.md) | Tolerancia de geometrías reales en capas versionadas | Fase 1 |
| [0053](0053-recuperacion-geometrias-invalidas-crudas.md) | Recuperación de geometrías inválidas crudas | Fase 2 |
| [0054](0054-tiles-mvt-on-the-fly-versionados.md) | Tiles MVT on-the-fly versionados | Fase 2 |
| [0055](0055-visor-blazor-maplibre-versionado.md) | Visor Blazor WebAssembly con MapLibre y tiles versionados | Fase 2 |
| [0056](0056-busqueda-y-ficha-predial-versionada.md) | Búsqueda y ficha predial sobre la versión activa | Fase 2 |
| [0057](0057-croquis-imprimible-cliente-svg.md) | Croquis imprimible en cliente con geometría planar SVG | Fase 2 |
| [0058](0058-politica-de-ramas-y-releases.md) | Política de ramas y releases | Fase 3.A |
| [0059](0059-geocodigo-ine-municipio-canonico.md) | Geocódigo INE como identificador municipal canónico | Fase 3.A |
| [0060](0060-pipeline-data-driven-por-esquema-municipal.md) | Pipeline de importación dirigido por esquema municipal | Fase 3.A |
| [0061](0061-m-lector-2-anillos-no-cerrados-y-equivalencia-proyeccion-esri.md) | M-LECTOR-2: anillos no cerrados y equivalencia de proyección ESRI | Fase 3.A |
| [0062](0062-visor-multimunicipio-data-driven-y-hot-swap.md) | Visor multimunicipio data-driven y hot-swap | Fase 3.A |
| [0063](0063-semantica-importacion-snapshot-completo-confirmado.md) | Confirmación del snapshot municipal completo como única semántica de importación versionada | Fase 3.B |
| [0064](0064-estatus-capa-predial-caranavi-carril-no-fotografiados.md) | Estatus de la capa predial de Caranavi: carril `PrediosNoFotografiados` como estado transitorio | Fase 3.B |
| [0065](0065-arranque-local-reconstruye-y-digest-estable.md) | El arranque local reconstruye imágenes y estabiliza su digest | Infraestructura |
| [0066](0066-motor-valuacion-terreno-rm024-2024.md) | Motor de valuación de terreno conforme a RM 024/2024 | Fase 4.A |
| [0067](0067-no-fijacion-vz-uyuni.md) | No se fija valor zonal imponible para Uyuni con la evidencia disponible | Fase 4.A |
