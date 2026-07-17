# ADR 0061 — M-LECTOR-2: anillos no cerrados y equivalencia de proyección ESRI

**Fecha**: 2026-07-16
**Estado**: Aceptado
**Relación**: continúa ADR 0053 (M-LECTOR-1) y ADR 0060.

## Contexto

La primera importación real de Caranavi (`022001`) produjo 391 observaciones
O1 en `MANZANOS_PROY`, frente a 78 invalideces medidas directamente sobre la
fuente. El diagnóstico reproducible determinó que 371 de sus 637 registros
contienen anillos cuyo último punto no repite el primero. Esta forma es
tolerada por herramientas del ecosistema GIS, pero no satisface la condición
de cierre exigida por `LinearRing`.

El lector estricto de NetTopologySuite.IO.Esri.Shapefile 1.2.0 rechaza esos
registros. Su modo `IgnoreInvalidShapes` usa deliberadamente un
`InvalidLinearRing` formado por cuatro coordenadas `(0,0)` cuando no puede
construir el anillo. El comportamiento está visible en la fuente del paquete:

- [`InvalidLinearRing` en las líneas 12-18](https://github.com/NetTopologySuite/NetTopologySuite.IO.Esri/blob/v1.2/src/NetTopologySuite.IO.Esri.Shapefile/Shp/Readers/ShpPolygonReader.cs#L12-L18).
- [sustitución del anillo inválido en las líneas 297-314](https://github.com/NetTopologySuite/NetTopologySuite.IO.Esri/blob/v1.2/src/NetTopologySuite.IO.Esri.Shapefile/Shp/Readers/ShpPolygonReader.cs#L297-L314).

Además, el `.prj` de Caranavi expresa WGS 84 / UTM zona 19S mediante WKT ESRI
sin código de autoridad. `LeerProyeccion` lo parseaba con `AuthorityCode = -1`
y creaba una transformación UTM 19S a UTM 19S. Esa operación innecesaria
transformaba el centinela `(0,0)` en
`(500000.0000000005, 2035.0569780916)` y perturbaba todas las coordenadas por
redondeo numérico, aun cuando origen y destino eran equivalentes.

El patrón M-LECTOR-1, formalizado en ADR 0053, exige conservar las geometrías
inválidas y sus razones reales para O1. ADR 0060 generaliza O1 a las capas
presentes del esquema municipal; por ello no es aceptable convertir un defecto
del lector en una observación de calidad atribuida a la fuente.

## Decisión

### Reparación estructural mínima

Cuando el lector estricto falla en un registro poligonal, el sistema lee el
registro indicado por SHX directamente desde los bytes SHP y reconstruye sus
partes con `GeometryFactory`. Para cada anillo aplica una única regla:

> Si el último punto no es exactamente igual en XY al primero, se agrega una
> copia exacta del primer punto como nuevo último punto.

La invariante es: **cerrar agrega un punto y modifica cero coordenadas
existentes**. No se normaliza orientación, no se mueve, elimina ni combina
vértices, y no se ejecuta reparación topológica. Los anillos que ya estaban
cerrados se reconstruyen sin cambios. Así, una auto-intersección u otra
invalidez real continúa persistida y PostGIS conserva su `ST_IsValidReason`
para O1.

La lectura directa se implementa localmente sobre el formato SHX/SHP porque
el constructor poligonal de bajo nivel del paquete es interno. No se agrega
otra dependencia. El lector `IgnoreInvalidShapes` permanece como último
recurso para registros que la reconstrucción no pueda interpretar. Si ese
camino produce el centinela de cuatro puntos idénticos, el límite de lectura
lo convierte en geometría `null` con `ErrorGeometria`; el centinela nunca se
persiste.

### Rechazo de reparaciones topológicas

No se usan `QuickFixInvalidShapes`, `FixInvalidShapes`, `GeometryFixer`,
`Buffer(0)` ni equivalentes. Esas operaciones pueden cambiar la forma y la
topología recibidas, ocultar las 78 invalideces reales de Caranavi y falsear
la evidencia que el informe de calidad debe devolver al GAM. M-LECTOR-2
resuelve únicamente la precondición estructural de cierre necesaria para
representar y evaluar el dato fuente.

### Equivalencia del WKT ESRI

Cuando el WKT carece de código de autoridad, `LeerProyeccion` reconoce
EPSG:32719 por la combinación completa de parámetros: proyección Transverse
Mercator, meridiano central `-69`, falso este `500000`, falso norte
`10000000`, factor de escala `0.9996`, latitud de origen `0`, unidad metro,
meridiano de Greenwich y elipsoide WGS 84.

Si todos coinciden, no se crea transformación y solo se asigna SRID 32719. Un
CRS genuinamente distinto continúa transformándose al SRID base. Si el WKT no
puede identificarse ni por autoridad ni por parámetros, se conserva la
semántica de proyección desconocida. Evitar una transformación identidad
elimina una deriva numérica que no aporta información ni corrección.

## Consecuencias

- Los 371 anillos abiertos pueden representarse sin introducir coordenadas
  fantasma; una nueva importación debe dejar visibles las 78 invalideces reales
  medidas sobre la fuente.
- O1 vuelve a describir defectos topológicos del dato recibido y no artefactos
  del fallback del lector.
- Las coordenadas de un SHP ya expresado en UTM 19S permanecen idénticas bit a
  bit después de la lectura y reciben SRID 32719.
- Los CRS distintos y las proyecciones no identificables conservan su
  comportamiento anterior.
- Un registro poligonal irrecuperable se degrada de forma explícita a `null` +
  `ErrorGeometria`; no se fabrica una geometría persistible.
- El dataset de Caranavi ya cargado no se modifica. La corrección toma efecto
  únicamente en una importación nueva controlada por el orquestador.
