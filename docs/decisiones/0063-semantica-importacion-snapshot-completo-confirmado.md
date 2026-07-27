# ADR 0063 — Confirmación del snapshot municipal completo como única semántica de importación versionada

**Fecha**: 2026-07-26
**Estado**: Aceptado
**Autores**: Saul Gutierrez + equipo del proyecto
**Relacionado**: ADR 0049 (confirmado, no derogado), ADR 0060, ADR 0051

---

## Contexto

Durante la fase 3.B se generó la capa `PRE_NOF_CAR` de Caranavi (`022001`) y se
intentó importarla como paquete de una sola capa. El paquete fue rechazado.

El reconocimiento de solo lectura del 26 de julio de 2026 estableció, contra
código y base de datos, lo siguiente:

1. `POST /api/importaciones/versiones` acepta únicamente los campos multipart
   `municipio_codigo` y `paquete`
   (`ImportacionesController.cs`, método `CrearVersion`). No admite selección de
   perfil.
2. El esquema municipal se resuelve **tres veces de forma independiente**:
   en el handler previo a la cola (`CrearVersionImportacionHandler`), en el
   worker asíncrono (`CargaVersionadaServicio`) y en la activación
   (`ActivacionVersionServicio`). La cola transporta únicamente un `Guid`.
3. `InspectorPaqueteVersionado` **sí** evalúa `Obligatoria`: una capa opcional
   ausente por completo no invalida el paquete. Las tres capas base de Caranavi
   son obligatorias; el rechazo se produjo por su ausencia, no por la presencia
   de una cuarta capa.
4. La activación **archiva** la versión activa anterior y activa la nueva. No
   copia ni reasigna filas `capa_*` hacia adelante. Una versión de una sola capa
   dejaría el resto del dataset municipal fuera de la versión activa.

Volumetría medida el 26 de julio de 2026:

| Municipio | Versión | Estado | Filas `capa_*` |
|---|---|---|---|
| Caranavi `022001` | v1 | PreviewListo | (huérfana, ver Consecuencias) |
| Caranavi `022001` | v2 | Activa | 687 |
| Uyuni `051201` | v2 | Archivada | ~35.000 |
| Uyuni `051201` | v3 | Activa | 35.013 |

Tamaño total de las nueve tablas `capa_*`: aproximadamente 40 MB, de los cuales
`capa_parcelas` ocupa 19 MB correspondientes a las dos copias retenidas de las
11.985 parcelas de Uyuni.

## Decisión

Se **confirma** ADR 0049 sin modificación: toda importación versionada
representa un snapshot municipal completo del esquema declarado para ese
municipio. No se añade parámetro de perfil al endpoint ni ningún mecanismo de
importación de capa individual.

Se descartan explícitamente las dos alternativas evaluadas:

**A1 — Composición N+1 materializada.** El sistema copiaría hacia la versión
nueva las capas no entregadas. Descartada porque **no reduce el almacenamiento**:
el coste por versión sigue siendo la copia íntegra (35.013 filas para Uyuni,
idéntico a B). Su único beneficio es evitar al operador reunir shapefiles sin
cambios, y ese beneficio no compensa el coste de implementar copia transaccional
por capa, metadatos de procedencia por capa y semántica de preview para capas
heredadas. Sin metadato de procedencia se violaría además el motivo declarado de
ADR 0049 —mezclar capas de fechas distintas sin poder saber cuáles— aunque se
respetara su letra.

**A2 — Composición por referencia.** La versión heredaría punteros a capas de
versiones previas en lugar de copiarlas. Es la única opción con ahorro real
(~35.000 filas por versión de Uyuni), pero elimina la premisa de que toda ruta de
lectura filtra por `dataset_version_id`. Afectaría a `TileVectorialService`,
`ConsultaPredioVersionado`, `ExtensionMunicipalService` y al croquis, todos
sellados en fases 2 y 3.A. Se difiere: no es una decisión de fase 3.B.

## Justificación

El argumento económico para cambiar la semántica es débil mientras el coste de
almacenamiento de A1 sea idéntico al de B, y el argumento de conveniencia
operativa no está respaldado por experiencia: el proyecto no ha completado
todavía un segundo ciclo de actualización real con un GAM. Cambiar el contrato de
importación antes de tener esa fricción medida sería diseñar contra una hipótesis.

## Consecuencias

- Para incorporar `PRE_NOF_CAR` a Caranavi se ensambla un paquete de cuatro
  capas a partir del contenido de `caranavi_v1.zip` más los archivos de
  `PRE_NOF_CAR`. No se requiere cambio de backend.
- **Contrato del paquete, verificado en código**: los archivos deben estar en la
  raíz del ZIP, sin carpeta contenedora, con el nombre base exacto declarado en
  `dominio.esquemas_capas`, y las extensiones `.shp`, `.dbf`, `.shx`, `.prj`
  (`InspectorPaqueteVersionado.ExtensionesRequeridas`). Extensiones adicionales
  como `.cpg`, `.sbn` o `.sbx` se ignoran sin error.
- La activación destructiva es **reversible**: `ActivacionVersionServicio`
  admite activar una versión en estado `Archivada` mediante
  `ReactivarDesdeArchivada`. Una activación errónea se revierte reactivando la
  versión anterior.
- **Deuda registrada — deriva de esquema.** `DatasetVersion` persiste
  `MunicipioCodigo` pero **no** el esquema ni los perfiles con los que fue
  inspeccionada. El esquema de Caranavi pasó de tres a cuatro capas declaradas
  después de que su v2 fuera activada; hoy no es posible reconstruir desde la
  fila de `dataset_versiones` con qué esquema se validó. Se recomienda persistir
  un snapshot del esquema en la versión. No se aborda en esta fase.
- **Deuda registrada — versiones huérfanas.** Caranavi v1 permanece en
  `PreviewListo` reteniendo filas `capa_*` y su objeto en MinIO. El estado
  `PreviewListo` es descartable vía
  `POST /api/importaciones/versiones/{id}/descartar`, que purga las filas pero
  **no** elimina el objeto de MinIO (ninguna ruta lo hace). Consistente con la
  deuda ya registrada en ADR 0035.
- Cada versión retenida cuesta su copia íntegra en disco. Con el ritmo actual el
  coste es despreciable; deberá reevaluarse antes de operar varios municipios del
  tamaño de Uyuni con historial largo.
