# Fase 3.B.6 — Generación del shapefile PRE_NOF_CAR

**Fecha de ejecución:** 24 de julio de 2026
**Ejecutor:** orquestador (PostGIS del contenedor sg_postgres + GDAL/OSGeo4W)
**Entrada:** `fase3b_tmp.padron_final` (6.573) y `fase3b_tmp.tripleta_parcial`
(3.B.4)
**Salida:** `PRE_NOF_CAR.shp` (+ .shx, .dbf, .prj, .cpg) en el directorio de
datos, fuera del repositorio

## Resultado del gate: SUPERADO

Shapefile conforme al contrato del perfil de importación
`caranavi-versionado-predios-no-fotografiados` (commit c96672c):

- Feature Count: 6.573 (= padrón final de 3.B.3)
- Campos DBF: `cod_pred` (Integer), `cod_geo` (String) — únicamente
- cod_pred poblado: 4.486; nulo: 2.087 (exportados como celda VACÍA, no 0)
- cod_geo: "022001" (geocódigo INE de Caranavi) en las 6.573 filas
- CRS: EPSG:32719 (UTM 19S) declarado en el .prj
- Geometría: multipolígonos preservados (el shapefile los declara como
  "Polygon" a nivel de tipo, pero las partes se conservan en los registros)
- Extent: (641933,9 · 8235474,9) – (663806,6 · 8258478,0), limpio, sin
  coordenadas fuera del rango UTM boliviano

Verificación crítica del nulo: `COUNT(cod_pred)` sobre el DBF dio 4.486 (no
6.573), confirmando que los predios sin numeración quedaron con cod_pred
vacío. El cargador los leerá como NULL vía `AEnteroOpcional`, sin
interpretarlos como cod_pred = 0.

## Contrato del shapefile (verificado contra el backend)

El reconocimiento del cargador y la referencia Uyuni establecieron el
contrato mínimo:

- El DBF incluye SOLO las columnas con dato para esta fase: `cod_pred` y
  `cod_geo`. `cod_uv` y `cod_man` se OMITEN por completo (no se crean
  vacías ni con centinela). El lector de la capa
  (`ShapefileLectorPredios.LeerCampoOpcional`) trata una columna ausente
  como celda vacía → NULL, de modo que el mapeo opcional del perfil las deja
  nulas sin error. Es el mismo patrón del fixture de Uyuni.
- `cod_pred` = prefijo numérico del texto de numeración (regla de 3.B.4).
  El sufijo de subdivisión (727 predios) NO viaja al shapefile:
  `CapaPredioNoFotografiado` no tiene propiedad para alojarlo. Queda
  preservado en la evidencia `fase3b_tmp.tripleta_parcial.sufijo` como
  refinamiento futuro.
- `cod_geo` = "022001", constante, opcional en el perfil.

## Secuencia ejecutada

Convención de ventanas y credenciales según el documento de 3.B.2 (OSGeo4W
para ogr2ogr/ogrinfo con PGPASSWORD por entorno; PowerShell para
scripts\sql.ps1 desde la raíz del repo).

### 1. Vista de exportación (PowerShell)

Un registro por predio del padrón final, con cod_pred de la tripleta parcial
y cod_geo constante:

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE VIEW fase3b_tmp.export_predios AS SELECT t.cod_pred, '022001'::text AS cod_geo, p.geom FROM fase3b_tmp.padron_final p JOIN fase3b_tmp.tripleta_parcial t ON t.fid_predio = p.fid_gpkg;"

Verificación: 6.573 filas, 4.486 con cod_pred.

### 2. Exportación a shapefile (OSGeo4W)

    ogr2ogr -f "ESRI Shapefile" C:\Proyectos\SG_SAUL_CATASTRO_DATOS\PRE_NOF_CAR.shp PG:"host=<PG_HOST> port=<PG_PORT> dbname=<PG_DB> user=<PG_USER>" fase3b_tmp.export_predios -nln PRE_NOF_CAR -nlt MULTIPOLYGON -lco ENCODING=UTF-8 -a_srs EPSG:32719

### 3. Verificación del DBF (OSGeo4W)

    ogrinfo -so C:\Proyectos\SG_SAUL_CATASTRO_DATOS\PRE_NOF_CAR.shp PRE_NOF_CAR
    ogrinfo -q -dialect SQLITE -sql "SELECT COUNT(*) AS total, COUNT(cod_pred) AS con_pred, SUM(CASE WHEN cod_geo='022001' THEN 1 ELSE 0 END) AS geo_ok FROM PRE_NOF_CAR" C:\Proyectos\SG_SAUL_CATASTRO_DATOS\PRE_NOF_CAR.shp

Resultado: total 6.573, con_pred 4.486, geo_ok 6.573.

## Estado de la fase tras 3.B.6

El paquete PRE_NOF_CAR (shp/shx/dbf/prj/cpg) está listo para importación.
Trabajo de datos de la fase 3.B completo: DGN → extracción → saneo →
numeración → tripleta parcial → shapefile conforme.

Siguiente y última sub-etapa: 3.B.7 — importación por el pipeline
multi-municipio (perfil predios-no-fotografiados de Caranavi), preview,
activación, y verificación de que las capas ya activas de Caranavi
(manzanas, áreas urbanas, puntos geodésicos) se conservan.
