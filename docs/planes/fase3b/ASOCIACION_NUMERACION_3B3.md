# Fase 3.B.3 — Asociación de numeración predial (cod_pred) de Caranavi

**Fecha de ejecución:** 24 de julio de 2026
**Ejecutor:** orquestador (PostGIS del contenedor sg_postgres + GDAL/OSGeo4W)
**Entradas:** `fase3b_tmp.padron_saneado` (3.B.2, 6.946) y
`fase3b_niveles.gpkg` capa `nivel06_numeracion` (3.B.1)
**Salidas (evidencia viva en `fase3b_tmp`):** `padron_final` (6.573 predios
únicos), `asignacion` (5.812 textos con su predio o huérfanos), más las
tablas intermedias del linaje (`nivel06_crudo`, `join_numeracion`,
`padron_dedup`, `pares_mutuos`, `dedup_mutuo`)

## Resultado del gate: SUPERADO

| Grupo | n | % del padrón final |
|---|---:|---:|
| Predios con exactamente un texto (cod_pred asignado) | 4.504 | 68,5 % |
| — de los cuales con numeración numérica pura | 3.759 | 57,2 % |
| — de los cuales con sufijo alfanumérico (1a, 4b, …) | 745 | 11,3 % |
| Predios con 2+ textos (ambigüedad real) | 226 | 3,4 % |
| Predios sin texto (componente de campo) | 1.843 | 28,0 % |
| Textos huérfanos (no caen en ningún predio) | 742 de 5.812 | — |

Control aritmético: 4.504 + 226 + 1.843 = 6.573 exacto.

**Número comercial de la fase: 4.504 predios (68,5 %) con numeración predial
asignada por evidencia espacial**, sobre un padrón real de 6.573 predios
únicos. Reproduce y supera la línea base del diagnóstico (4.708/6.950 =
67,7 %): numerador y denominador más honestos tras deduplicación.

## Hallazgo estructural: el padrón fuente tiene 5,4 % de duplicación interna

El join bruto (6.779 filas para 5.812 textos) reveló textos contenidos en
hasta 8 predios simultáneamente. La disección con umbrales de solape mutuo
distinguió dos poblaciones:

1. **Duplicación**: 230 features con geometría idéntica byte a byte (177
   grupos: 144 dobles, 14 triples, 18 cuádruples, 1 quíntuple) más 143
   redibujos casi idénticos (solape mutuo ≥ 90 % del área de AMBOS, 137
   clusters por componentes conexas). Total: 373 features redundantes —
   el mismo sector dibujado varias veces al ensamblar el DGN por
   urbanizaciones. Padrón: 6.946 → 6.573.
2. **Contención (paraguas)**: 1.234 pares donde un lote está ≥ 90 % dentro
   de un predio mayor sin ser su duplicado. Los paraguas permanecen en el
   padrón; la regla de asignación les impide capturar textos ajenos.
   Refinamiento diferido: marcar como posible no-predio a todo polígono que
   contenga 3 o más predios finales.

Validación de la deduplicación: los textos huérfanos quedaron en 742 antes
y después de deduplicar — ningún cluster eliminó un representante que
contuviera textos, es decir, la dedup no destruyó cobertura.

## Reglas de asignación aplicadas

- Textos = los 5.812 POINT con `Text` no vacío del nivel 6 (verificado:
  ninguna LINESTRING/POLYGON del nivel porta texto). Join por
  `ST_Contains(predio, punto)` — el criterio de centroide/PointOnSurface
  canonizado aplica a polígonos contenidos; para puntos la contención es
  directa.
- Multi-contención resuelta por **contenedor mínimo envolvente**: cada texto
  se asigna al predio de MENOR área entre los que lo contienen
  (`DISTINCT ON ... ORDER BY ST_Area ASC`), conforme a la regla canonizada
  en 17-ter. Resultado: 5.812 asignaciones únicas, cero multiplicidad.
- Los 1.060 textos no numéricos (18 %) NO se excluyeron del join: la
  numericidad es un atributo del resultado, no un filtro de entrada. Son
  mayormente numeración con sufijo de subdivisión (1a, 4b); solo residuos
  como "AREA" o "Capacitación" son rótulos ajenos, visibles en el grupo de
  ambiguos o huérfanos.
- Convenciones de origen coexistentes: "01" y "1" son textos distintos que
  colapsan al mismo entero cod_pred. La unicidad del triplete dependerá de
  cod_man; anotado para las etapas siguientes.

## Encoding de la fuente (resuelto en la carga)

Los textos del DGN llegan al GPKG con bytes de codepage Windows sin
declarar; la carga a PostgreSQL (UTF-8 estricto) aborta con "Non UTF-8
content". Resolución: `set PGCLIENTENCODING=LATIN1` antes del ogr2ogr y
limpieza de la variable después. Verificado: 9 textos con caracteres no
ASCII, todos interpretables tras conversión (numeraciones con tilde de
subdivisión tipo "1.B´" y un rótulo "Capacitación").

## Secuencia ejecutada

Convención de ventanas y credenciales según el documento de 3.B.2
(OSGeo4W para ogr2ogr/ogrinfo con PGPASSWORD por entorno; PowerShell para
scripts\sql.ps1 desde la raíz del repo).

### 1. Carga de la numeración (OSGeo4W)

    set PGCLIENTENCODING=LATIN1
    ogr2ogr -f PostgreSQL PG:"host=<PG_HOST> port=<PG_PORT> dbname=<PG_DB> user=<PG_USER>" C:\Proyectos\SG_SAUL_CATASTRO_DATOS\fase3b_niveles.gpkg nivel06_numeracion -nln fase3b_tmp.nivel06_crudo -lco GEOMETRY_NAME=geom -lco FID=fid_gpkg -preserve_fid
    set PGCLIENTENCODING=

Verificación: 9.027 features (5.812 POINT con texto + 3.176 LINESTRING +
37 POLYGON + 2 MULTILINESTRING sin texto).

### 2. Índices y join bruto (PowerShell)

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE INDEX IF NOT EXISTS ix_tmp_padron_geom ON fase3b_tmp.padron_saneado USING gist (geom); CREATE INDEX IF NOT EXISTS ix_tmp_nivel06_geom ON fase3b_tmp.nivel06_crudo USING gist (geom); ANALYZE fase3b_tmp.padron_saneado; ANALYZE fase3b_tmp.nivel06_crudo;"

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE TABLE fase3b_tmp.join_numeracion AS SELECT t.fid_gpkg AS fid_texto, TRIM(t.text) AS texto, TRIM(t.text) ~ '^[0-9]+$' AS es_numerico, p.fid_gpkg AS fid_predio FROM fase3b_tmp.nivel06_crudo t LEFT JOIN fase3b_tmp.padron_saneado p ON ST_Contains(p.geom, t.geom) WHERE t.text IS NOT NULL AND TRIM(t.text) <> '';"

El join bruto (6.779 filas) reprodujo la línea base del diagnóstico
(4.711/314/1.921/742 vs 4.708/—/1.926/— del diagnóstico) y reveló la
multi-contención que motivó la disección.

### 3. Deduplicación (PowerShell)

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE TABLE fase3b_tmp.padron_dedup AS SELECT DISTINCT ON (ST_AsBinary(geom)) fid_gpkg, type_dgn, level_dgn, text_dgn, era_valida, area_previa, geom FROM fase3b_tmp.padron_saneado ORDER BY ST_AsBinary(geom), fid_gpkg;"

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE TABLE fase3b_tmp.pares_mutuos AS SELECT a.fid_gpkg AS fa, b.fid_gpkg AS fb FROM fase3b_tmp.padron_dedup a JOIN fase3b_tmp.padron_dedup b ON a.fid_gpkg < b.fid_gpkg AND a.geom && b.geom AND ST_Relate(a.geom, b.geom, 'T********') AND ST_Area(ST_Intersection(a.geom, b.geom)) >= 0.9 * GREATEST(ST_Area(a.geom), ST_Area(b.geom));"

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE TABLE fase3b_tmp.dedup_mutuo AS WITH RECURSIVE aristas AS (SELECT fa, fb FROM fase3b_tmp.pares_mutuos UNION SELECT fb, fa FROM fase3b_tmp.pares_mutuos), alcance (nodo, rep) AS (SELECT DISTINCT fa, fa FROM aristas UNION SELECT a.fb, alcance.rep FROM alcance JOIN aristas a ON a.fa = alcance.nodo) SELECT nodo, MIN(rep) AS representante FROM alcance GROUP BY nodo; CREATE TABLE fase3b_tmp.padron_final AS SELECT p.* FROM fase3b_tmp.padron_dedup p WHERE NOT EXISTS (SELECT 1 FROM fase3b_tmp.dedup_mutuo m WHERE m.nodo = p.fid_gpkg AND m.representante <> p.fid_gpkg); CREATE INDEX ix_tmp_padron_final_geom ON fase3b_tmp.padron_final USING gist (geom); ANALYZE fase3b_tmp.padron_final;"

Resultados: 6.946 → 6.716 (dedup exacta) → 6.573 (dedup de mutuos,
146 pares en 137 clusters). Representante por grupo: menor fid_gpkg.

### 4. Asignación con contenedor mínimo y gate (PowerShell)

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE TABLE fase3b_tmp.asignacion AS SELECT DISTINCT ON (t.fid_gpkg) t.fid_gpkg AS fid_texto, TRIM(t.text) AS texto, TRIM(t.text) ~ '^[0-9]+$' AS es_numerico, p.fid_gpkg AS fid_predio, ST_Area(p.geom) AS area_predio FROM fase3b_tmp.nivel06_crudo t LEFT JOIN fase3b_tmp.padron_final p ON ST_Contains(p.geom, t.geom) WHERE t.text IS NOT NULL AND TRIM(t.text) <> '' ORDER BY t.fid_gpkg, ST_Area(p.geom) ASC NULLS LAST;"

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "SELECT 'predios_con_1_texto' AS grupo, COUNT(*) AS n FROM (SELECT fid_predio FROM fase3b_tmp.asignacion WHERE fid_predio IS NOT NULL GROUP BY fid_predio HAVING COUNT(*) = 1) a UNION ALL SELECT 'predios_ambiguos_2mas', COUNT(*) FROM (SELECT fid_predio FROM fase3b_tmp.asignacion WHERE fid_predio IS NOT NULL GROUP BY fid_predio HAVING COUNT(*) > 1) b UNION ALL SELECT 'predios_sin_texto', (SELECT COUNT(*) FROM fase3b_tmp.padron_final) - COUNT(DISTINCT fid_predio) FROM fase3b_tmp.asignacion WHERE fid_predio IS NOT NULL UNION ALL SELECT 'textos_huerfanos', COUNT(*) FROM fase3b_tmp.asignacion WHERE fid_predio IS NULL;"

## Cosecha pendiente para AGENTS.md 17-ter

Tres trampas de esta ejecución: (a) los textos de DGN llegan al GPKG con
bytes de codepage Windows sin declarar — la carga a PostgreSQL aborta;
declarar PGCLIENTENCODING=LATIN1 en la conversión, limpiar la variable
después, y verificar los textos convertidos antes de usarlos en joins;
(b) en padrones DGN ensamblados por urbanizaciones, verificar duplicación
interna ANTES de cualquier join o conteo: geometría idéntica (dedup por WKB)
y redibujos (solape mutuo ≥ 90 % de ambas áreas, clustering por componentes
conexas) — un join sobre padrón con duplicados infla asignaciones y
ambigüedades; (c) al deduplicar, validar contra los huérfanos del join: si
el conteo de huérfanos sube tras la dedup, se eliminó un representante que
contenía textos — con conteo estable, la dedup no destruyó cobertura.
