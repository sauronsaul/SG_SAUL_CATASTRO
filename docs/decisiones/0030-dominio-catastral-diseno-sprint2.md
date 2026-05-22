# ADR 0030 — Diseño del Dominio Catastral (Sprint 2 Checkpoint 2.1)

**Fecha**: 2026-05-18
**Estado**: Aceptado
**Autores**: Saul Gutierrez + Claude Code

---

## Contexto

Inicio del Sprint 2: implementación del dominio catastral central (`Predio`, `Propietario`,
`CodigoCatastral`). Varias decisiones de diseño fueron discutidas y aprobadas antes de la
implementación. Este ADR las documenta en conjunto.

---

## Decisiones

### 1. NetTopologySuite en SG.Domain

**Decisión**: Se agrega `NetTopologySuite 2.6.0` como única dependencia externa de `SG.Domain`.

**Justificación**: NTS es una biblioteca de geometría matemática pura (equivalente a
`System.Numerics` para vectores). No tiene dependencias de I/O ni de infraestructura.
El tipo `Polygon` de NTS es un tipo de valor del dominio catastral, no un detalle de
persistencia. Ocultarlo detrás de una interfaz añadiría complejidad sin beneficio real.

**Consecuencia**: La excepción queda documentada en `SG.Domain.csproj`. Toda otra
dependencia requiere autorización explícita en ADR.

---

### 2. Código catastral PROVISIONAL (CodigoCatastral VO)

**Formato aprobado**: `{DEP(2)}-{PROV(3)}-{MUN(3)}-{ZONA(3)}-{MZN(4)}-{LOTE(4)}`

**Ejemplo**: `02-004-001-001-0001-0001`

**Acepta**: entrada con o sin guiones. Normaliza a forma canónica con guiones (24 chars).

**Persistencia**: columna `codigo_catastral VARCHAR(24) UNIQUE NOT NULL` vía value converter
(`CodigoCatastral.Valor` → string, string → `CodigoCatastral.FromDb`).

**Nota PROVISIONAL**: El formato está sujeto a validación oficial por la entidad competente
(ver CLAUDE.md sección 15). El VO protege el cambio: si el formato oficial varía, solo
cambia `CodigoCatastral` y la migración correspondiente.

---

### 3. Multi-municipio por instalaciones separadas

**Decisión**: NO hay `municipio_id` en ninguna tabla. Cada municipio tiene su propia
instalación Docker.

**Justificación**: Modo local single-instance. Simplifica el modelo enormemente.
Migraciones multi-tenant son una complejidad prematura para el MVP.

---

### 4. UbicacionCatastral como Value Object owned inline

**Decisión**: `UbicacionCatastral` se mapea con `OwnsOne` sin tabla separada.
Columnas: `ubic_zona`, `ubic_manzana`, `ubic_lote`, `ubic_barrio`, `ubic_direccion`,
`ubic_referencia` en la tabla `dominio.predios`.

**Justificación**: La ubicación es inseparable del predio. Una tabla separada para datos
que nunca se consultan independientemente añade complejidad sin beneficio.

---

### 5. GeometriaPredial como Value Object owned nullable

**Decisión**: `GeometriaPredial` (VO con `Polygon SRID=32719`) se mapea como
`OwnsOne` nullable. Columna: `geometria geometry(Polygon, 32719)` en `dominio.predios`.

**Justificación**: Un predio puede registrarse sin geometría (ficha catastral primero,
digitalización después). La geometría es nullable en el dominio y en la BD.

**Invariante**: SRID 32719 (UTM WGS84 Zona 19S) validado en el constructor del VO.

---

### 6. UsoSuelo como catálogo en BD (no enum)

**Decisión**: `UsoSuelo` es una entidad `AggregateRoot` en tabla `dominio.usos_suelo`.
13 valores semilla para Caranavi cargados por `DomainSeeder`.

**Justificación**: Los valores pueden ampliarse sin migración. El municipio puede necesitar
tipos propios. Un enum requeriría recompilación para agregar valores.

---

### 7. Máquina de estados del predio

**Estado inicial**: `Borrador`

**Transiciones válidas**:
- `Borrador` → `EnRevision` (`EnviarARevision`)
- `EnRevision` → `Validado` (`Validar`)
- `EnRevision` → `Observado` (`Observar`, requiere texto de observaciones)
- `Observado` → `Borrador` (`RetornarBorrador`)

**HistorialEstado**: registro INMUTABLE de cada transición. No tiene `updated_at`,
`is_deleted` ni método de modificación. Ver tabla `dominio.historial_estados`.

---

### 8. Documento: solo soft delete, nunca eliminación física

**Decisión**: `Documento.Eliminar()` marca `IsDeleted = true` con `EliminadoAt`,
`EliminadoPor` y `MotivoEliminacion`. La fila en BD y el objeto en MinIO **nunca**
se eliminan físicamente. Los documentos eliminados van a `papelera/` en MinIO
(responsabilidad del handler en Sprint 2.2).

**Justificación**: Trazabilidad institucional. Un documento eliminado por error debe
poder recuperarse. Los municipios operan bajo normativa de conservación documental.

---

### 9. RelacionPredioPropietario con historial temporal

**Decisión**: Una relación tiene `vigente_desde` y `vigente_hasta` (nullable = actual).
Cerrar una relación no la elimina; se registra `vigente_hasta`. La suma de `porcentaje`
de relaciones vigentes no puede superar 100%.

---

### 10. RowVersion vía xmin (PostgreSQL)

**Decisión**: El campo `RowVersion` de `AggregateRoot` se mapea con `.IsRowVersion()`
que en Npgsql se traduce a la columna del sistema `xmin` (tipo `xid`). No se genera
una columna adicional. PostgreSQL actualiza `xmin` automáticamente en cada UPDATE.

---

## Tablas creadas en M003_Dominio_Inicial

| Tabla | Schema | Descripción |
|---|---|---|
| `usos_suelo` | `dominio` | Catálogo de uso de suelo (13 valores Caranavi) |
| `propietarios` | `dominio` | Personas naturales y jurídicas |
| `predios` | `dominio` | Agregado raíz del catastro |
| `documentos` | `dominio` | Adjuntos del predio (soft delete) |
| `historial_estados` | `dominio` | Transiciones de estado (inmutable) |
| `relaciones_predio_propietario` | `dominio` | Titularidad con historial |

---

## Alternativas descartadas

| Alternativa | Motivo |
|---|---|
| `EstadoPredio` como string libre | Sin control de transiciones válidas en el dominio |
| `Documento` con eliminación física | Pierde trazabilidad, incumple conservación documental |
| `CodigoCatastral` como string simple | Sin validación de formato; cambiarlo después es invasivo |
| Tabla separada para `UbicacionCatastral` | Complejidad innecesaria para datos inseparables del predio |
| enum para `TipoDerecho` / `TipoDocumento` | Limita extensibilidad sin recompilación |
