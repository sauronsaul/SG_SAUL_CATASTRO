# Fase 3.B.2 — Saneo del padrón predial de Caranavi

**Fecha de ejecución:** 22 de julio de 2026
**Ejecutor:** orquestador (PostGIS del contenedor sg_postgres + GDAL/OSGeo4W)
**Entrada:** `fase3b_niveles.gpkg`, capa `nivel04_padron` (3.B.1)
**Salida:** `fase3b_padron.gpkg`, capa `padron_saneado_v2` — 6.946
multipolígonos, 0 geometrías inválidas
**Evidencia viva:** esquema `fase3b_tmp` en la base local (padron_crudo,
padron_excluidos, padron_saneado)

## Resultado del gate: SUPERADO

Balance: 6.950 polígonos de entrada → 4 exclusiones nominales de origen →
6.946 saneados, 0 inválidas, 0 vaciados, tipo homogéneo MULTIPOLYGON.

Exclusiones nominales (geometría degenerada de origen: "too few points",
área 0; irrecuperables, registradas en `fase3b_tmp.padron_excluidos`):
fid 20999, 22298, 22318, 56291.

Casos nominales documentados:
- **fid 20961** — área 610,03 → 305,02 m² (delta = exactamente la mitad).
  Anillo plegado sobre sí mismo: el contorno inválido recorría dos veces la
  zona y `ST_Area` sobre la geometría inválida contaba el área duplicada.
  El valor saneado (305 m², 7 vértices, perímetro 91,5 m, proporciones de
  lote real) es el área verdadera; 610 era artefacto de la invalidez. Regla
  general: sobre geometría inválida, `ST_Area` no es confiable — los deltas
  de saneo se interpretan con esto en mente.
- **fid 113672** — la primera pasada de `ST_MakeValid` preservó el área
  (955,92 m², delta 0) pero dejó una micro-autointersección residual.
  Segunda pasada quirúrgica del mismo saneo sobre esa única fila la
  resolvió con área intacta. Regla general: verificar `ST_IsValid` DESPUÉS
  del saneo; una pasada no garantiza validez en el 100 % de los casos.

## Por qué el saneo canónico es PostGIS y no GEOS/SQLite

La primera corrida se intentó con `ST_MakeValid` vía `-dialect SQLITE`
(GEOS legacy embebido en GDAL/QGIS 3.4x, estrategia "linework"). Resultado
inaceptable y descartado: de las 33 inválidas, 11 predios quedaron vaciados
(área → 0, colapsados a líneas/colecciones; ~6.771 m² de padrón destruidos),
2 con geometría nula, y la capa resultante mezcló GEOMETRYCOLLECTION,
LINESTRING y nulos, con errores "Overlay input is mixed-dimension".

El saneo canónico es PostGIS (3.4.3, GEOS 3.12) con
`ST_MakeValid(geom, 'method=structure')`: reconstruye la intención areal en
lugar de descomponer el linework. Las 11 víctimas de GEOS legacy
sobrevivieron con área exacta (verificado fila a fila, delta 0,00).
`ST_CollectionExtract(_, 3)` + `ST_Multi` como red de seguridad y
homogeneización de tipo.

## Secuencia ejecutada

Convención de ventanas (trampa operativa verificada): en esta máquina
coexisten dos GDAL — el de OSGeo4W Shell (completo, con driver PostgreSQL)
y otro en el PATH de PowerShell (sin él). Todo `ogr2ogr`/`ogrinfo` que toque
PostgreSQL se ejecuta en la OSGeo4W Shell. Los `scripts\sql.ps1` se ejecutan
en PowerShell desde la raíz del repo (leen la conexión del `.env`).

Los parámetros `<PG_*>` de los comandos ogr2ogr se toman del `.env` raíz:
POSTGRES_HOST, POSTGRES_PORT, POSTGRES_DB, POSTGRES_USER, POSTGRES_PASSWORD.
La contraseña no se incluye en la cadena de conexión: se exporta como
variable de entorno PGPASSWORD en la OSGeo4W Shell antes de ejecutar
(`set PGPASSWORD=<valor del .env>`), de modo que ni el comando versionado
ni el historial de la shell la contienen. Nunca se versionan valores
literales de credenciales.

### 0. Sonda de capacidad (PowerShell)

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "SELECT postgis_full_version();"
    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "SELECT ST_IsValid(ST_MakeValid(ST_GeomFromText('POLYGON((0 0,10 0,0 10,10 10,0 0))'), 'method=structure')) AS estructura_ok;"

Verificado: POSTGIS 3.4.3, GEOS 3.12.2; el polígono de prueba se reconstruye
en MULTIPOLYGON válido.

### 1. Aislamiento del padrón (OSGeo4W) y carga a PostGIS

    ogr2ogr -f GPKG C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_padron.gpkg C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_niveles.gpkg -nln padron_crudo -dialect SQLITE -sql "SELECT * FROM nivel04_padron WHERE GeometryType(geom) IN ('POLYGON','MULTIPOLYGON')"

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE SCHEMA IF NOT EXISTS fase3b_tmp;"

    ogr2ogr -f PostgreSQL PG:"host=<PG_HOST> port=<PG_PORT> dbname=<PG_DB> user=<PG_USER>" C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_padron.gpkg padron_crudo -nln fase3b_tmp.padron_crudo -lco GEOMETRY_NAME=geom -lco FID=fid_gpkg -preserve_fid

`-preserve_fid` mantiene los fid del GPKG como identificadores trazables.
ogr2ogr normaliza los nombres de columna a minúsculas al cargar a PostgreSQL
(verificado vía information_schema antes de escribir SQL contra la tabla).

### 2. Censo y exclusiones (PowerShell)

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "SELECT COUNT(*) AS total, SUM(CASE WHEN NOT ST_IsValid(geom) THEN 1 ELSE 0 END) AS invalidas FROM fase3b_tmp.padron_crudo;"

Resultado: 6.950 / 33 (coincide con la línea base del diagnóstico y con el
censo previo vía GEOS — la invalidez es propiedad de la fuente, estable
entre estadios).

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE TABLE fase3b_tmp.padron_excluidos AS SELECT fid_gpkg, ST_Area(geom) AS area_m2, 'geometria degenerada de origen: too few points, area 0' AS razon FROM fase3b_tmp.padron_crudo WHERE NOT ST_IsValid(geom) AND ST_Area(geom) = 0; SELECT * FROM fase3b_tmp.padron_excluidos ORDER BY fid_gpkg;"

### 3. Saneo (PowerShell)

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE TABLE fase3b_tmp.padron_saneado AS SELECT fid_gpkg, type AS type_dgn, level AS level_dgn, text AS text_dgn, ST_IsValid(geom) AS era_valida, ST_Area(geom) AS area_previa, ST_Multi(ST_CollectionExtract(ST_MakeValid(geom, 'method=structure'), 3)) AS geom FROM fase3b_tmp.padron_crudo WHERE fid_gpkg NOT IN (SELECT fid_gpkg FROM fase3b_tmp.padron_excluidos); SELECT COUNT(*) AS saneados FROM fase3b_tmp.padron_saneado;"

Resultado: 6.946. Segunda pasada quirúrgica sobre la única inválida
residual (fid 113672):

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "UPDATE fase3b_tmp.padron_saneado SET geom = ST_Multi(ST_CollectionExtract(ST_MakeValid(geom, 'method=structure'), 3)) WHERE NOT ST_IsValid(geom);"

### 4. Gate (PowerShell)

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "SELECT COUNT(*) AS total, SUM(CASE WHEN NOT ST_IsValid(geom) THEN 1 ELSE 0 END) AS aun_invalidas, SUM(CASE WHEN ST_IsEmpty(geom) OR ST_Area(geom) = 0 THEN 1 ELSE 0 END) AS vaciados FROM fase3b_tmp.padron_saneado;"
    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "SELECT fid_gpkg, ROUND(area_previa::numeric,2) AS area_previa, ROUND(ST_Area(geom)::numeric,2) AS area_nueva, ROUND(ABS(ST_Area(geom)-area_previa)::numeric,2) AS delta_m2 FROM fase3b_tmp.padron_saneado WHERE NOT era_valida ORDER BY delta_m2 DESC;"
    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "SELECT GeometryType(geom) AS tipo, COUNT(*) AS n FROM fase3b_tmp.padron_saneado GROUP BY 1;"

Resultados: 6.946 / 0 / 0; deltas todos 0,00 salvo los casos nominales
20961 (pliegue, documentado arriba) y 4835/21976/121393/21838/56032
(sub-métricos, correcciones legítimas de anillo); tipo único MULTIPOLYGON.

### 5. Export y verificación final (OSGeo4W)

    ogr2ogr -f GPKG -update C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_padron.gpkg PG:"host=<PG_HOST> port=<PG_PORT> dbname=<PG_DB> user=<PG_USER>" fase3b_tmp.padron_saneado -nln padron_saneado_v2 -nlt MULTIPOLYGON

    ogrinfo -q -dialect SQLITE -sql "SELECT COUNT(*) AS n, SUM(CASE WHEN NOT ST_IsValid(geom) THEN 1 ELSE 0 END) AS invalidas FROM padron_saneado_v2" C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_padron.gpkg

Resultado: 6.946 / 0. La capa `padron_saneado` (v1, corrida GEOS fallida)
dentro del mismo GPKG queda como evidencia de la corrida descartada y no se
usa; la capa de trabajo de la fase es `padron_saneado_v2`.

## Cosecha pendiente para AGENTS.md 17-ter

Cinco trampas de esta ejecución, a canonizar en la próxima cosecha:
(a) `ST_MakeValid` vía GEOS legacy (SQLite dialect / QGIS 3.4x) destruye
geometrías en modo linework — el saneo canónico es PostGIS
`method=structure` con `ST_CollectionExtract(_, 3)` de red; (b) sobre
geometría inválida `ST_Area` no es confiable (anillos plegados duplican
área): los deltas de saneo se interpretan tras entender la invalidez, no
antes; (c) el saneo se verifica con `ST_IsValid` DESPUÉS de ejecutarse y se
repite quirúrgicamente sobre residuales — una pasada no garantiza el 100 %;
(d) en máquinas con múltiples GDAL, el driver PostgreSQL puede existir solo
en uno: verificar la ventana/instalación antes de diagnosticar errores de
conexión; (e) gitleaks dispara sobre el patrón `password=` en cadenas de
conexión aunque el valor sea un placeholder — los comandos versionados usan
la variable de entorno PGPASSWORD, nunca `password=` inline.
