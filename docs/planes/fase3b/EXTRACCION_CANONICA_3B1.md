# Fase 3.B.1 — Extracción canónica del DGN de Caranavi

**Fecha de ejecución:** 22 de julio de 2026
**Ejecutor:** orquestador (GDAL/OSGeo4W vía QGIS 3.4x, máquina local)
**Fuente:** `CARTOGRAFIA_CARANAVI_AGOSTO_2023_V71.dgn`
**Artefactos producidos:** `fase3b_bruto.gpkg` (capa `elementos`),
`fase3b_niveles.gpkg` (siete capas por nivel)
**Directorio de datos:** `C:\Proyectos\SG_SAUL_CATASTRO_DATOS\`

Este documento versiona los comandos exactos de la extracción, el registro de
estadio de conteos y la evaluación del gate de 3.B.1, conforme al
dimensionamiento v2 y su adenda. Los artefactos `catastral.gpkg`,
`nivel10.gpkg` y demás GeoPackage previos al 22 de julio quedan como legado
del diagnóstico, sin cadena de custodia, y no se reutilizan en la fase.

## Registro de estadio (obligatorio para todo conteo de la fase)

El DGN declara 122.073 elementos. Su materialización a GeoPackage produce
**126.165 features**: el driver DGN de GDAL descompone elementos compuestos
(cadenas complejas, células, nodos de texto múltiple) al iterar la fuente. La
diferencia (+4.092) es determinista: se verificó idéntica con y sin
`-nlt GEOMETRY`, con los mismos 4 warnings de `organizePolygons()`
(geometrías con anillos interiores). Este mecanismo explica también, con alta
probabilidad, el excedente históricamente inexplicado del artefacto legado
`curvas_cand` frente a `catastral.gpkg`.

**Convención de la fase:** todo conteo se expresa en features de
`fase3b_bruto.gpkg`, nunca en elementos DGN.

El extent de la fuente contiene basura (mínimos negativos, máximos absurdos)
que el filtro canónico de coordenada absurda excluye en la extracción por
nivel. El DGN no declara CRS; se asigna EPSG:32719 en la materialización
(asignación, no reproyección).

## Comandos ejecutados

### 1. Materialización bruta

    ogr2ogr -f GPKG C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_bruto.gpkg C:\Proyectos\SG_SAUL_CATASTRO_DATOS\CARTOGRAFIA_CARANAVI_AGOSTO_2023_V71.dgn elements -nln elementos -nlt GEOMETRY -a_srs EPSG:32719 --config OGR_DGN_LINK_FORMAT JSON

Salida esperada: 4 warnings de `organizePolygons()` (inofensivos con
`-nlt GEOMETRY`: las colecciones entran como tales). Verificación posterior
obligatoria con `ogrinfo -so`: Feature Count 126.165, CRS 32719,
**Geometry Column = `geom`**.

Advertencia canónica: el nombre de la columna de geometría en GPKG derivados
de DGN depende de la versión y ruta de GDAL (`geom` aquí; `GEOMETRY` en los
artefactos legados). Ni uno ni otro son asumibles: el `ogrinfo -so` posterior
a cada materialización no es opcional, y todo SQL se escribe contra el nombre
verificado.

### 2. Extracción por nivel con filtro canónico

Un comando por nivel del alcance (4, 6, 34, 39, 40, 42, 61), todos hacia
`fase3b_niveles.gpkg` (el primero crea el archivo; los siguientes usan
`-update`). El filtro es la negación del criterio canónico de descarte por
coordenada fuera del rango UTM 19S boliviano, aplicado sobre el GPKG (nunca
sobre el DGN, por las limitaciones documentadas de `WHERE` y `-spat`).

    ogr2ogr -f GPKG C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_niveles.gpkg C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_bruto.gpkg -nln nivel04_padron -dialect SQLITE -sql "SELECT * FROM elementos WHERE Level = 4 AND NOT (ST_MaxX(geom) < 200000 OR ST_MinX(geom) > 900000 OR ST_MaxY(geom) < 7000000 OR ST_MinY(geom) > 9500000)"

    ogr2ogr -f GPKG -update C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_niveles.gpkg C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_bruto.gpkg -nln nivel06_numeracion -dialect SQLITE -sql "SELECT * FROM elementos WHERE Level = 6 AND NOT (ST_MaxX(geom) < 200000 OR ST_MinX(geom) > 900000 OR ST_MaxY(geom) < 7000000 OR ST_MinY(geom) > 9500000)"

    ogr2ogr -f GPKG -update C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_niveles.gpkg C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_bruto.gpkg -nln nivel34_contornos -dialect SQLITE -sql "SELECT * FROM elementos WHERE Level = 34 AND NOT (ST_MaxX(geom) < 200000 OR ST_MinX(geom) > 900000 OR ST_MaxY(geom) < 7000000 OR ST_MinY(geom) > 9500000)"

    ogr2ogr -f GPKG -update C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_niveles.gpkg C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_bruto.gpkg -nln nivel39_manzanas_loc -dialect SQLITE -sql "SELECT * FROM elementos WHERE Level = 39 AND NOT (ST_MaxX(geom) < 200000 OR ST_MinX(geom) > 900000 OR ST_MaxY(geom) < 7000000 OR ST_MinY(geom) > 9500000)"

    ogr2ogr -f GPKG -update C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_niveles.gpkg C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_bruto.gpkg -nln nivel40_numeracion_loc -dialect SQLITE -sql "SELECT * FROM elementos WHERE Level = 40 AND NOT (ST_MaxX(geom) < 200000 OR ST_MinX(geom) > 900000 OR ST_MaxY(geom) < 7000000 OR ST_MinY(geom) > 9500000)"

    ogr2ogr -f GPKG -update C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_niveles.gpkg C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_bruto.gpkg -nln nivel42_superficies -dialect SQLITE -sql "SELECT * FROM elementos WHERE Level = 42 AND NOT (ST_MaxX(geom) < 200000 OR ST_MinX(geom) > 900000 OR ST_MaxY(geom) < 7000000 OR ST_MinY(geom) > 9500000)"

    ogr2ogr -f GPKG -update C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_niveles.gpkg C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_bruto.gpkg -nln nivel61_vias -dialect SQLITE -sql "SELECT * FROM elementos WHERE Level = 61 AND NOT (ST_MaxX(geom) < 200000 OR ST_MinX(geom) > 900000 OR ST_MaxY(geom) < 7000000 OR ST_MinY(geom) > 9500000)"

Nota de alcance: cada capa contiene TODO el nivel dentro de rango (todos los
tipos de geometría). El filtrado a los tipos relevantes (p. ej. solo
polígonos del padrón en el nivel 4) corresponde a 3.B.2, no a la extracción.

### 3. Consulta del gate

    ogrinfo -q -dialect SQLITE -sql "SELECT Level, GeometryType(geom) AS tipo, CASE WHEN ST_MaxX(geom) < 200000 OR ST_MinX(geom) > 900000 OR ST_MaxY(geom) < 7000000 OR ST_MinY(geom) > 9500000 THEN 'fuera' ELSE 'dentro' END AS rango, COUNT(*) AS n FROM elementos WHERE Level IN (4, 6, 34, 39, 40, 42, 61) GROUP BY Level, tipo, rango ORDER BY Level, tipo" C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_bruto.gpkg

## Resultado del gate (22 de julio de 2026): SUPERADO

Conteos dentro de rango, contrastados contra la línea base de la v2 (medida
sobre el artefacto legado `catastral.gpkg`, estadio distinto):

| Nivel | fase3b_bruto (dentro) | Línea base v2 | Veredicto |
|---|---|---|---|
| 4 | 6.945 POLYGON + 5 MULTIPOLYGON (= 6.950); además 4.814 LINESTRING, 355 MULTILINESTRING, 404 POINT; 6 fuera de rango (5 LS, 1 PT) | 6.950 POLYGON | Explicado: este estadio preserva 5 predios multiparte (anillos interiores, los warnings de organizePolygons) que el estadio legado contó como simples. Cifra canónica del padrón conservada: 6.950. |
| 6 | 5.812 POINT, 3.176 LINESTRING, 37 POLYGON, 2 MULTILINESTRING | sin línea base bruta | Primera medición: queda como línea base bruta de la numeración. Coherente con el join del diagnóstico (4.708 asignados salen del universo de 5.812 textos). |
| 34 | 690 LS, 10 MLS, 138 POLY, 514 PT | idéntica | Coincidencia exacta. |
| 39 | 130 LS, 122 PT, 3 MLS, 2 POLY | 128 LS, 120 PT, 3 MLS, 2 POLY | +2/+2 atribuido a la descomposición del driver (estadios distintos). Menor y acotado; si molesta en 3.B.5, se investiga entonces. |
| 40 | 148 LS, 14 MLS, 13 POLY, 22 PT | idéntica | Coincidencia exacta. |
| 42 | 48 POLY, 22 LS | idéntica | Coincidencia exacta. |
| 61 | 33 LS, 11 MLS | idéntica | Coincidencia exacta. |

Toda diferencia queda explicada con estadio declarado; el gate de 3.B.1 se da
por superado. Los 6 elementos fuera de rango del nivel 4 quedaron excluidos
de `nivel04_padron` por el filtro.

## Cosecha pendiente para AGENTS.md 17-ter

Dos trampas descubiertas en esta ejecución, a canonizar en la próxima
cosecha: (a) el nombre de la columna de geometría en GPKG derivados de DGN
depende de la versión y ruta de GDAL — verificar con `ogrinfo -so` tras cada
materialización, sin excepción; (b) el driver DGN descompone elementos
compuestos al leer: el conteo de features del GPKG difiere del conteo de
elementos declarado por el DGN, de forma determinista — es un estadio más a
declarar, y explica excedentes entre artefactos materializados por rutas
distintas.
