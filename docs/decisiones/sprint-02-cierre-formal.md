# Sprint 2 — Cierre Formal

**Fecha de cierre**: 2026-05-21  
**Autor**: Saul Gutierrez  
**Tag de release**: `v0.2.0-mvp-dominio` (pendiente de creación)

---

## Objetivo del Sprint 2

Implementar el dominio catastral central (`Predio`, `Propietario`, `CodigoCatastral`)
con máquina de estados, historial de titularidad, documentos adjuntos y suite de tests
de dominio ≥ 80% de cobertura efectiva.

**Entregable verificable**: Tests de dominio en verde con ≥ 80% de cobertura +
CRUD de predios y propietarios funcional via API REST.

---

## Tabla consolidada de checkpoints

| Checkpoint | Entregables principales | ADRs creados |
|---|---|---|
| **2.1** — Dominio T1–T5 | `CodigoCatastral` VO, `Predio` + máquina estados, `Propietario`, `RelacionPredioPropietario`, `UbicacionCatastral`, `GeometriaPredial`, catálogos (`UsoSuelo`), migración M003, `DomainSeeder`, tests dominio (66 tests) | 0030, 0031, 0032 |
| **2.2** — API CRUD + bugs | Controllers `PrediosController` + `PropietariosController`, handlers CQRS completos, `DocumentoAdjunto`, corrección Bug 2 (`ValueGeneratedNever()`), migración M004 (renombre catálogo), migración M005 (`codigo_catastral` nullable) | 0033 |
| **2.3** — Tests integración | 14 tests E2E (`PredioE2ETests`), corrección Bug A (`Database.SqlQuery<bool>`), corrección Bug B (`SgApiFactory` connection string leak), total 100 tests en verde | 0034 |

---

## Estado del sistema al cierre del Sprint 2

### Migraciones aplicadas

| Migración | Fecha | Descripción |
|---|---|---|
| M001 | 2026-05-12 | Identidad + auditoría (Sprint 1) |
| M002 | 2026-05-14 | RefreshToken.RevokedReason (Sprint 1) |
| M003 | 2026-05-18 | Dominio inicial: predios, propietarios, relaciones, documentos, historial |
| M004 | 2026-05-18 | Renombre `usos_suelo` → `catalogo_uso_suelo` |
| M005 | 2026-05-18 | `codigo_catastral` nullable (asignado al validar, no al crear) |

### Endpoints catastrales operativos

| Endpoint | Descripción | Estado |
|---|---|---|
| `POST /api/predios` | Crear predio en estado Borrador | ✓ |
| `GET /api/predios` | Listar predios (paginado) | ✓ |
| `GET /api/predios/{id}` | Obtener predio por ID | ✓ |
| `PUT /api/predios/{id}/propietario` | Vincular propietario con % y tipo de derecho | ✓ |
| `DELETE /api/predios/{id}/propietario/{propId}` | Cerrar relación propietario | ✓ |
| `POST /api/predios/{id}/documentos` | Subir documento adjunto (multipart) | ✓ |
| `POST /api/predios/{id}/estado/enviar-revision` | Transición Borrador → EnRevision | ✓ |
| `POST /api/predios/{id}/estado/validar` | Transición EnRevision → Validado + asigna código catastral | ✓ |
| `POST /api/predios/{id}/estado/observar` | Transición EnRevision → Observado | ✓ |
| `POST /api/predios/{id}/estado/retornar-borrador` | Transición Observado → Borrador | ✓ |
| `GET /api/predios/{id}/historial` | Historial de estados (solo Admin) | ✓ |
| `POST /api/propietarios/persona-natural` | Crear persona natural | ✓ |
| `POST /api/propietarios/persona-juridica` | Crear persona jurídica | ✓ |

### Suite de tests al cierre

| Proyecto | Tests | Descripción |
|---|---|---|
| `SG.Domain.Tests` | 66 | Dominio puro: Predio, Propietario, CodigoCatastral, GeometriaPredial, RelacionPredioPropietario |
| `SG.Application.Tests` | 20 | Handlers de autenticación (Sprint 1, sin cambios) |
| `SG.Api.IntegrationTests` | 14 | E2E: flujo completo P1-P10, autorización, unicidad, regresión Bug 2 |
| **Total** | **100** | 0 errores, 0 warnings (`TreatWarningsAsErrors = true`) |

### Cobertura efectiva de dominio

- `SG.Domain.Tests`: 66 tests cubriendo todas las rutas de lógica real en `Predio`,
  `Propietario`, `CodigoCatastral`, `GeometriaPredial` y `RelacionPredioPropietario`.
- Cobertura reportada ≥ 80% en clases con lógica (excluyendo entidades de solo datos y
  records de error conforme ADR 0027).

### ADRs documentados en Sprint 2

`0030`, `0031`, `0032`, `0033`, `0034` — 5 decisiones técnicas nuevas.

**Total acumulado proyecto**: 24 ADRs (`0001`, `0005`–`0007`, `0011`–`0019`, `0025`–`0034`).

---

## Entidades del dominio implementadas

| Entidad / VO | Tipo | Descripción |
|---|---|---|
| `Predio` | AggregateRoot | Raíz del catastro con máquina de estados |
| `CodigoCatastral` | Value Object | Formato `02-006-028-ZZZ-MMMM-LLLL`, validado, normalizado |
| `UbicacionCatastral` | Value Object (OwnsOne) | zona + manzana + lote + barrio + dirección + referencia |
| `GeometriaPredial` | Value Object (OwnsOne, nullable) | Polygon SRID=32719 (UTM Zona 19S) |
| `HistorialEstado` | Entity (inmutable) | Registro de cada transición de estado |
| `Documento` | Entity (soft delete) | Adjunto con referencia a MinIO |
| `Propietario` | AggregateRoot | Persona natural o jurídica |
| `RelacionPredioPropietario` | Entity (historial temporal) | Titularidad con vigencia y porcentaje |
| `UsoSuelo` | Catalog entity | 13 valores Caranavi, cargados por DomainSeeder |

### Máquina de estados del Predio

```
Borrador → EnRevision → Validado
                     ↘ Observado → Borrador
```

---

## Bugs corregidos en Sprint 2

| Bug | Causa | Fix | ADR |
|---|---|---|---|
| **Bug 1** | M003 tenía `codigo_catastral NOT NULL` pero se asigna al validar (no al crear) | M005: hacer nullable la columna | — (implícito en M005) |
| **Bug 2** | EF Core emitía UPDATE en vez de INSERT para entidades hijas con PK autogenerada en dominio | `ValueGeneratedNever()` en `RelacionPredioPropietario`, `Documento`, `HistorialEstado` | 0033 |
| **Bug A** | `ExisteCodigoCatastralAsync` LINQ no traducía VO a SQL correctamente | `Database.SqlQuery<bool>` con `FormattableString` | 0034 |
| **Bug B** | `SgApiFactory` apuntaba a BD local por captura prematura de connection string en `AddPersistencia` | `RemoveAll<DbContextOptions> + AddDbContext` en `SgApiFactory` | 0034 |

---

## Deuda técnica aceptada al cierre del Sprint 2

| Item | ADR / Referencia | Sprint objetivo |
|---|---|---|
| Tipos de derecho faltantes: `Ocupante`, `Copropietario`, `Solicitante`, `RepresentanteLegal` | ADR 0031 | Sprint 3 |
| `DocumentoAdjunto`: mover archivo a `papelera/` en MinIO al hacer soft delete | ADR 0030 sección 8 | Sprint 2.2+ |
| Permisos finos por claim (actualmente solo `Admin` vs `Tecnico`) | CLAUDE.md sección 10 | Sprint 3 |
| Doble interceptor en `SgApiFactory` (EF Core aplica interceptores dos veces en tests) | ADR 0034 | Sprint 3 (si molesta) |
| Validación oficial del formato `CodigoCatastral` por entidad competente | CLAUDE.md sección 15 | Antes de producción |

---

## Criterio de cierre del Sprint 2

El Sprint 2 se cierra **APROBADO** con todos los criterios de aceptación cumplidos:

- ✓ Tests de dominio: 66 tests en verde, cobertura efectiva ≥ 80%
- ✓ API CRUD de predios y propietarios funcional
- ✓ Máquina de estados del predio con historial inmutable
- ✓ Tests de integración E2E: 14 tests en verde (flujo completo + autorización + unicidad + regresión)
- ✓ Total: 100 tests, 0 errores, 0 warnings
- ✓ 5 migraciones aplicadas, esquema de BD estable

**Próximo sprint**: Sprint 3 — API REST completa, Swagger, autorización por claims, tramites.
