# Sprint 3 — Cierre Formal

**Estado**: APROBADO
**Fecha**: 2026-06-01
**Autor**: Saul Gutierrez
**Tag**: `v0.3.0-importacion-shapefile`

---

## Objetivo del Sprint 3

Implementar el módulo de **importación masiva de shapefiles** para el
sistema catastral municipal, permitiendo cargar, previsualizar,
confirmar y auditar la importación de predios y construcciones desde
archivos `.shp` provistos por el municipio. Entregable verificable:
importar el shapefile real del piloto Uyuni (11.985 predios + 18.484
construcciones) con detección idempotente de duplicados, respeto al
flujo de trabajo humano (estados `Validado`, `EnRevision`, `Observado`
no se sobrescriben), y trazabilidad completa vía auditoría.

## Tabla consolidada de tareas y checkpoints

### Tareas de implementación (T1–T13)

| Tarea | Entregable | ADR asociado |
|---|---|---|
| T1–T6 | Modelo de dominio de importación, infraestructura base | — |
| T7 | `GenerarPreviewImportacionHandler` (upload a MinIO + pipeline) | 0035, 0037 |
| T8 | `ConfirmarImportacionHandler` (transaccional + check-recompute) | 0036, 0037 |
| T9 | `PerfilImportacion` + mapeo configurable de columnas | — |
| T10 | Migraciones M006 (importación + construcción) y M007 (NombreArchivoShp) | — |
| T11 | Adaptadores externos: `ShapefileReader` (NTS), `ZipExtractor`, `MapeadorImportacion` | — |
| T12 | `ListarImportacionesHandler` (paginado) | — |
| T13 | `ObtenerDetalleImportacionHandler` (`GET /{id}`) | 0037 |

### Punto de Control 3.2 — Pruebas de validación end-to-end (P1–P8 + P6b)

| Prueba | Qué valida | Resultado |
|---|---|---|
| P1 | Preview predios Uyuni (11.985 filas, 0 rechazadas) | ✅ 200 OK, 2.2s |
| P2 | Confirmación predios (11.985 creados, IMPORTADO, auditoría 2/predio) | ✅ ~34s |
| P3 | Verificación BD (SRID 32719, historial 1/predio) | ✅ |
| P4 | Importación construcciones (18.484 filas → 18.423 creadas, 34 rech, 27 omit) | ✅ 22s |
| P5 | Re-importación → detecta 11.985 actualizar, 0 crear, 0 omitir | ✅ Idempotencia confirmada |
| P6 | Predio en `Validado` sobrevive intacto re-importación | ✅ 5/5 criterios verde |
| P6b | TOCTOU: confirmador recomputa estado vs preview | ✅ 6/6 criterios verde |
| P7 | Tabla `auditoria` sin geometría serializada | ✅ 0 bytes geométricos, max 642 B/registro |
| P8 | `GET /{id}` con volumen real (Confirmada vs PreviewGenerado) | ✅ con hallazgo `RegistrarResultados` |

## Estado del sistema al cierre

### Migraciones EF Core

| Migración | Contenido |
|---|---|
| **M006** `M006_Sprint3_Importacion_Construccion` | Tablas `importaciones`, `perfiles_importacion`, `mapeos_columna`, `equivalencias_valor`, `construcciones`. Columna `geometria` (Polygon SRID 32719) en `predios`. Valor `Importado` agregado al enum `EstadoPredio` |
| **M007** `M007_NombreArchivoShpPerfilImportacion` | Columna `NombreArchivoShp` en `perfiles_importacion` (distingue `.shp` dentro del mismo zip) |

### Endpoints HTTP nuevos

| Verbo | Ruta | Autorización |
|---|---|---|
| POST | `/api/importaciones/preview` | Admin, Tecnico |
| POST | `/api/importaciones/{id:guid}/confirmar` | Admin, Tecnico |
| GET | `/api/importaciones` | Admin, Tecnico |
| GET | `/api/importaciones/{id:guid}` | Admin, Tecnico |
| GET | `/api/perfiles-importacion` | Admin, Tecnico |

### Suite de tests

| Proyecto | Tests | Delta vs Sprint 2 |
|---|---:|---:|
| SG.Domain.Tests | 104 | +16 |
| SG.Application.Tests | 47 | +30 |
| SG.Api.IntegrationTests | 15 | +1 |
| **Total** | **166** | **+66** |

100% verdes, 0 omitidos, 0 fallos. Build Release: 0 errores, 0 warnings.

### ADRs documentados (7 nuevos)

| ADR | Líneas | Título |
|---|---:|---|
| 0035 | 75 | Deuda técnica: limpieza de previews huérfanos en MinIO |
| 0036 | 151 | Estrategia transaccional en `ConfirmarImportacionHandler` |
| 0037 | 72 | Semántica de los campos de conteos del agregado `Importacion` |
| 0038 | 135 | Auditoría correcta de entidades OwnsOne |
| 0039 | 117 | Rotación obligatoria de secretos tras filtración detectada |
| 0040 | 84 | Degradación de FluentAssertions de 8.9.0 a 7.2.2 |
| 0041 | 126 | Auditoría append-only e independiente del dominio |

Total proyecto: **31 ADRs** (24 previos + 7 nuevos).

## Entidades del dominio implementadas

| Entidad | Tipo | Función |
|---|---|---|
| `Importacion` | Agregado raíz (extendido) | Representa una operación de importación. Métodos refactorizados: `RegistrarConteosPreview`, `RegistrarConteosConfirmacion`, `Confirmar`, `MarcarFallida` (todos con guardas de estado) |
| `PerfilImportacion` | Agregado raíz | Configuración reutilizable de cómo mapear columnas de un shapefile a entidades del dominio |
| `MapeoColumna` | VO interno de `PerfilImportacion` | Asocia un campo del `.dbf` con una propiedad del dominio, marca obligatoriedad |
| `EquivalenciaValor` | VO interno de `PerfilImportacion` | Normaliza códigos del shapefile (ej. `"VIV" → "Vivienda"`) |
| `Construccion` | Entidad de dominio | Edificación vinculada a un predio padre por tripleta `(zona, manzana, lote)` |
| `GeometriaPredial` | OwnsOne de `Predio` | Encapsula el `Polygon` NTS (SRID 32719); opcional (predios sin geometría son válidos) |

Métodos nuevos del dominio:
- `Predio.ActualizarDesdeImportacion(...)` — operación idempotente que no toca campos controlados por flujo humano.
- `Predio.AsignarGeometria(Polygon)` — fija geometría desde el `.shp`.

## Bugs corregidos durante el sprint

| Bug | Causa | Fix | ADR |
|---|---|---|---|
| Inflación de auditoría 1/3 (~12.068 registros extra por importación) | `UbicacionCatastral` (`OwnsOne`) se auditaba como entidad standalone | Interceptor adopta `IsOwned()` genérico + fusión de propiedades del owned en el padre | 0038 |
| Geometría como blob JSON en `auditoria` | NTS `Polygon` serializaba coordenadas completas en `valor_nuevo` | `Poligono` excluido de `PropiedadesExcluidas` del interceptor (defensa en profundidad) | 0036, 0038 |
| Password `sg_postgres` filtrado dos veces | Política "secreto filtrado se regenera" (ADR 0018) era principio declarativo sin ciclo formal de ejecución; rotación quedaba como tarea futura tras documentar el incidente | Rotación obligatoria en la misma sesión de detección + validación del viejo como inválido + saneamiento del vector | 0039 |
| `FluentAssertions` 8.x bajo licencia comercial Xceed | Cambio de licencia upstream (MIT → comercial) | Degradación a 7.2.2 (última MIT); API idéntica para los usos del proyecto | 0040 |
| `Importacion.RegistrarResultados` polivalente con semántica ambigua | Único método llamado desde preview (proyección) y confirmación (resultado real); escribía mismos campos con significados opuestos | Separación en `RegistrarConteosPreview` + `RegistrarConteosConfirmacion`, ambos con guardas de estado | 0037 |
| Agregado `Importacion` anémico (sin guardas) | `Confirmar()` y `MarcarFallida()` podían invocarse en cualquier estado; permitía transiciones inválidas (ej. `Confirmada → Fallida`) | Guardas de estado en los 4 métodos del agregado + 10 tests unitarios cubriéndolas | 0037 |
| TOCTOU en ventana preview→confirmación | Snapshot trackeado por EF Core podría desincronizarse durante el `SaveChanges` final | Documentado como límite conocido (sin mitigación en Sprint 3, el piloto Uyuni opera con un único técnico) | 0036 |

## Hallazgos del Punto de Control 3.2

El Punto de Control 3.2 ejecutó 9 pruebas end-to-end contra base de datos
real con el shapefile completo del piloto Uyuni. Síntesis de hallazgos
no triviales:

**P5 — Idempotencia confirmada.** Re-importación de los mismos 11.985
predios produce 11.985 `Actualizar`, 0 `Crear`, 0 duplicados. La detección
de duplicados usa la tripleta `(Zona, Manzana, Lote)` del `OwnsOne
UbicacionCatastral` —el `codigo_catastral` quedó deliberadamente `NULL`
post-importación—.

**P6 — Respeto al flujo humano.** Un predio en estado `Validado` sobrevive
intacto a una re-importación de 11.985 filas. La rama `Omitir` del
confirmador no toca la entidad ni la marca como dirty en EF Core.

**P6b — TOCTOU verificado.** El confirmador recompute la acción desde BD
en la línea 135 de `ConfirmarCapaPrediosAsync` y NO confía en lo que dijo
el preview. Validar un predio entre preview y confirmación es respetado:
el preview marcó `Actualizar`, la confirmación lo omitió (`filasOmitidas=2`
en lugar de `1`). Límite conocido: concurrencia simultánea en la ventana
de ~10–14s del `SaveChanges` final no está cubierta (ver ADR 0036).

**P7 — Auditoría sin geometría.** 0 bytes de contenido geométrico
serializado sobre 127.728 registros de auditoría. Tamaño máximo por
registro: 642 bytes (sin blobs). 12.068 registros standalone de
`UbicacionCatastral` pre-fix (commit `2ba04c2`) sobrevivieron al reset
del schema `dominio` por ser auditoría append-only (ver ADR 0041).

**P8 — Defecto semántico detectado y mitigado.** El método único
`RegistrarResultados` se invocaba desde dos flujos con semántica opuesta
(proyección en preview, resultado real en confirmación). Importaciones
en estado `PreviewGenerado` mostraban `filas_importadas = 11985` cuando
semánticamente debían ser 0. Mitigación parcial aplicada en commit
`aabd874` (separación en dos métodos del dominio con guardas); el
renombrado de columnas persistidas queda como deuda explícita (ADR
0037, sección "Deuda pendiente").

## Deuda técnica aceptada

| # | Deuda | Origen | Sprint objetivo |
|---:|---|---|---|
| 1 | Job de limpieza de previews huérfanos en MinIO | ADR 0035 | Sprint 4 |
| 2 | Renombrar columnas `filas_importadas_*` para reflejar semántica preview vs confirmación | ADR 0037 | Sprint 4 |
| 3 | Mitigación de concurrencia simultánea TOCTOU (aislamiento, optimistic locking o re-lectura intra-transacción) | ADR 0036 | Sprint 4 (junto al rediseño asíncrono) |
| 4 | Importación asíncrona (202 Accepted + polling) para municipios > 20.000 predios | ADR 0036 | Sprint 4 / 5 |
| 5 | Configuración de timeout HTTP en Caddy para el endpoint de confirmación | ADR 0036 | Sprint 4 |
| 6 | Tests de integración del `AuditoriaInterceptor` (cubrir INSERT, UPDATE escalar, UPDATE solo-owned) | ADR 0038 | Sprint 4 |
| 7 | Migrar exclusión geométrica en interceptor de string literal `"Poligono"` a exclusión por tipo `IsAssignableFrom(Geometry)` | P7 hallazgo | Sprint 4 |
| 8 | Hook pre-commit (`git-secrets` o equivalente) para detectar passwords en diffs | ADR 0039 | Sprint 4 |
| 9 | Procedimiento de rotación de secretos documentado en `CONTRIBUTING.md` | ADR 0039 | Sprint 4 |
| 10 | Política unificada para errores de dominio: `DomainException` vs `Result<DomainError>` (asimetría detectada en P7) | P7 hallazgo | Sprint 4 |
| 11 | Particionamiento o archivado frío de `auditoria.auditoria` para municipios grandes (>50k predios anuales) | ADR 0041 | Sprint 5+ |
| 12 | Manual de operación incluyendo invariante append-only de auditoría | ADR 0041 | Sprint 4 |
| 13 | Procedimiento reproducible de reset de datos de prueba (script en repo) para que el entorno no dependa de estado manual acumulado | P7 hallazgo | Sprint 4 |

## Métricas del sprint

**Volumen de cambios** (21 commits, `origin/develop..HEAD`):
- 84 archivos (63 nuevos + 21 modificados)
- 8.161 líneas insertadas, 65 eliminadas
- Saldo neto: +8.096 líneas

**Commits por tipo**:

| Tipo | Cantidad | Líneas |
|---|---:|---:|
| `feat` | 7 | 5.355 |
| `docs(adr)` | 7 | 760 |
| `docs(repo)` | 1 | 471 |
| `test` | 1 | 718 |
| `refactor` | 1 | 856 |
| `fix` | 2 | 53 |
| `chore` | 2 | 20 |
| **Total** | **21** | **8.233 ins** |

**Cobertura de tests** (delta vs Sprint 2):
- SG.Domain.Tests: 88 → 104 (+16, principalmente del refactor `Importacion`)
- SG.Application.Tests: ~17 → 47 (+30, módulo de importación)
- SG.Api.IntegrationTests: 14 → 15 (+1, `AuditoriaInterceptorGeometriaTests`)
- **Total**: 100 → 166 (+66 tests, +66%)

**Cobertura por capa del módulo de importación**:
- Dominio: cubierto (agregados, métodos con guardas, validaciones)
- Aplicación: cubierto (mapeador, clasificador, handler de confirmación con dobles)
- Infraestructura: cubierto vía tests de integración del interceptor
- API: no cubierta por tests (verificación manual end-to-end en P1–P8 + P6b)

## Criterio de cierre

- [x] Build Release: 0 errores, 0 warnings
- [x] Suite de tests: 166/166 verdes
- [x] Punto de Control 3.2: 9/9 pruebas pasadas (P1, P2, P3, P4, P5, P6, P6b, P7, P8)
- [x] 7 ADRs nuevos commiteados (0035–0041)
- [x] Importación de Uyuni reproducible end-to-end (preview → confirmar → re-importar)
- [x] Idempotencia y respeto al flujo humano verificados con datos reales
- [x] Auditoría sin contaminación geométrica (verificado en BD real)
- [x] Working tree limpio, sin archivos sin commitear ni untracked
- [x] 21 commits semánticos sin trailers `Co-Authored-By`
- [x] 13 ítems de deuda técnica explícitamente documentados con sprint objetivo

**APROBADO** para tag `v0.3.0-importacion-shapefile`.

**Próximo sprint**: Sprint 4 — Mitigaciones de deuda crítica del Sprint 3
(renombre semántico de columnas, importación asíncrona, hook de secretos,
configuración HTTP de Caddy) y comienzo del frontend de operación
catastral municipal.
