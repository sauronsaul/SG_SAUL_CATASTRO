# Informe de calidad de datos cartográficos — Caranavi v1: hallazgos

**Dirigido a:** Gobierno Autónomo Municipal de Caranavi

**Fecha:** 17 de julio de 2026

**Estado:** primera edición del informe técnico

**Fuente:** dataset Caranavi (`022001`) con `estado = 'Activa'`, versión interna 2

> La denominación “v1” corresponde a la edición del informe, no al
> `numero_version` interno del dataset. La línea base operativa se seleccionó
> por estado `Activa`.

## 1. Alcance

Este informe presenta los hallazgos obtenidos durante la importación y
verificación de la primera entrega cartográfica de Caranavi incorporada al
sistema. El análisis comprende las capas de manzanos, áreas urbanas y puntos
geodésicos.

El sistema no corrige silenciosamente los datos fuente ni reemplaza decisiones
que corresponden al GAM. Cuando una condición estructural impedía leer un
registro, la importación aplicó únicamente la adaptación mínima documentada y
conservó las coordenadas recibidas. Los defectos topológicos y vacíos
alfanuméricos permanecen visibles para que el GAM pueda corregirlos en origen y
remitir una nueva entrega versionada.

## 2. Resumen de hallazgos

| Código | Hallazgo | Resultado verificado |
|---|---|---:|
| H1 | Anillos no cerrados en `MANZANOS_PROY.shp` | 371 de 637 |
| H2 | Manzanos con geometría auto-intersecante | 78 de 637 |
| H3 | Manzanos sin valor en el campo fuente `No_MANZANO` | 634 de 637 |
| H4 | Capa de lotes o predios | No incluida |
| H5 | Archivo `.prj` de `AREA_URBANA.shp` | No incluido |
| H6 | Capas útiles correctamente georreferenciadas | 637 manzanos, 17 áreas urbanas y 33 puntos geodésicos |

## 3. Hallazgos

### 3.1 H1 — Anillos no cerrados en la fuente

En `MANZANOS_PROY.shp`, 371 de los 637 registros presentaban anillos cuyo
primer vértice no estaba repetido como último vértice. Diversas herramientas
GIS de escritorio toleran esta forma, pero un anillo poligonal conforme
requiere cierre explícito.

Para hacer posible la lectura, la importación agregó una copia exacta del
primer punto al final de cada anillo abierto. Esta adaptación no movió, eliminó
ni modificó ninguna coordenada existente y no realizó reparación topológica.
La regla, el diagnóstico y sus límites están documentados en el ADR 0061,
`0061-m-lector-2-anillos-no-cerrados-y-equivalencia-proyeccion-esri.md`.

Este conteo corresponde al análisis del archivo fuente previo a la
reconstrucción. Después del cierre estructural, una consulta SQL sobre la
geometría persistida ya no puede distinguir qué anillos llegaron abiertos; por
ello H1 se respalda con la evidencia reproducible del proceso de importación y
no con una consulta posterior que produciría una conclusión distinta.

**Acción recomendada:** exportar los polígonos con anillos explícitamente
cerrados en futuras entregas.

### 3.2 H2 — Geometrías auto-intersecantes

Después de resolver únicamente el cierre estructural, 78 manzanos continúan
siendo inválidos según las reglas OGC. Las razones persistidas corresponden a
auto-intersecciones reales de la geometría fuente. El sistema las conserva tal
como fueron recibidas; no aplica `buffer(0)`, desplazamiento de vértices ni otra
reparación que cambie la forma.

Ejemplos:

| Fila de origen | Razón técnica (`ST_IsValidReason`) |
|---:|---|
| 26 | `Self-intersection[652305.785472045 8248930.96910413]` |
| 52 | `Self-intersection[652095.86131541 8249066.72337134]` |
| 53 | `Self-intersection[652191.366000153 8249051.70199986]` |
| 56 | `Self-intersection[652365.152887284 8249175.17098235]` |
| 58 | `Self-intersection[652490.757953459 8249147.40315507]` |

Las coordenadas de la razón técnica permiten ubicar el punto aproximado del
defecto en EPSG:32719.

**Acción recomendada:** revisar topológicamente los 78 objetos en la fuente,
corregir sus cruces de segmentos con criterio técnico municipal y volver a
exportar la capa.

### 3.3 H3 — Numeración de manzanos incompleta

El campo fuente `No_MANZANO` está vacío en 634 de los 637 manzanos. Solo tres
registros contienen número, con los valores `22`, `23` y `24`.

Sin numeración de manzano y sin una capa predial no es posible construir la
clave catastral completa distrito–manzano–predio a partir de esta entrega. El
sistema no asigna números por proximidad, orden de archivo ni posición
cartográfica, porque cualquiera de esas reglas inventaría información que debe
ser definida por el GAM.

**Acción recomendada:** completar `No_MANZANO` con la numeración institucional
vigente y asegurar que sea coherente con la futura capa de lotes o predios.

### 3.4 H4 — Ausencia de capa predial

El esquema municipal de la entrega activa contiene únicamente
`AreasUrbanas`, `Manzanas` y `PuntosGeodesicos`; no incluye una capa de lotes o
predios. Existe el antecedente de lotes en la cartografía DGN de agosto de
2023, pero esos objetos no fueron exportados como shapefile en esta entrega.

En consecuencia, el visor puede mostrar la cartografía disponible de
Caranavi, pero mantiene deshabilitados la búsqueda por clave catastral y la
ficha predial hasta recibir una capa predial vinculable.

**Acción recomendada:** exportar los lotes o predios desde la fuente municipal
a shapefile, incluyendo identificadores estables y la numeración necesaria
para relacionarlos con distrito y manzano.

### 3.5 H5 — Declaración de proyección ausente en áreas urbanas

`AREA_URBANA.shp` llegó sin su archivo acompañante `.prj`. Para la importación
controlada se utilizó la declaración de `MANZANOS_PROY.shp` después de
verificar que ambas capas comparten el sistema WGS 84 / UTM zona 19 Sur
(EPSG:32719).

Este procedimiento permitió procesar la entrega actual, pero no sustituye la
declaración de proyección que debe acompañar cada capa: archivos con nombres o
coordenadas similares pueden pertenecer a sistemas distintos, y asumir el CRS
sin verificación puede desplazar toda la cartografía.

**Acción recomendada:** incluir el `.prj` correspondiente para cada shapefile
en futuras entregas.

### 3.6 H6 — Aspectos correctos y aprovechables

La entrega presenta una base espacial útil y consistente:

| Capa | Registros | Con geometría | SRID |
|---|---:|---:|---:|
| Manzanos | 637 | 637 | 32719 |
| Áreas urbanas | 17 | 17 | 32719 |
| Puntos geodésicos | 33 | 33 | 32719 |

Los 33 puntos geodésicos contienen identificadores no vacíos y distintos. Las
17 geometrías de áreas urbanas y los 637 manzanos están disponibles para
visualización municipal. La georreferenciación de las tres capas es correcta y
consistente en EPSG:32719.

Esta base permite conservar el contexto urbano y el control geodésico mientras
se completa la información predial.

## 4. Implicación de calidad y diseño

Los hallazgos no invalidan el valor cartográfico de la entrega: la
georreferenciación, los puntos de control, los manzanos y las áreas urbanas ya
permiten una visualización municipal coherente. Sin embargo, la numeración
incompleta y la ausencia de lotes impiden pasar de un mapa de referencia a un
catastro predial consultable.

Para habilitar el catastro predial pleno, se solicita que una futura entrega
incluya:

1. capa de lotes o predios, con geometrías y numeración institucional;
2. manzanos con el campo `No_MANZANO` completo y validado;
3. identificadores que permitan relacionar cada predio con su distrito y
   manzano;
4. corrección en origen de las 78 auto-intersecciones;
5. anillos poligonales explícitamente cerrados;
6. archivos `.prj` para todas las capas;
7. conservación de los 33 puntos geodésicos y sus identificadores como
   referencia de control.

Las correcciones deben realizarse en la fuente municipal y entregarse como una
nueva versión. El sistema conservará la trazabilidad entre la versión actual y
la futura.

## 5. Consultas reproducibles

Todas las consultas seleccionan la línea base por
`municipio_codigo = '022001' AND estado = 'Activa'`; no suponen un número de
versión.

### 5.1 Identidad de la versión activa

```sql
SELECT id, municipio_codigo, numero_version, estado
FROM dominio.dataset_versiones
WHERE municipio_codigo = '022001'
  AND estado = 'Activa';
```

### 5.2 H2 — Conteo y ejemplos de geometrías inválidas

```sql
WITH activa AS (
    SELECT id
    FROM dominio.dataset_versiones
    WHERE municipio_codigo = '022001'
      AND estado = 'Activa'
)
SELECT count(*) AS invalidas
FROM dominio.capa_manzanas cm
JOIN activa a ON a.id = cm.dataset_version_id
WHERE cm.geometria IS NOT NULL
  AND NOT ST_IsValid(cm.geometria);
```

```sql
WITH activa AS (
    SELECT id
    FROM dominio.dataset_versiones
    WHERE municipio_codigo = '022001'
      AND estado = 'Activa'
)
SELECT cm.fila_origen,
       ST_IsValidReason(cm.geometria) AS razon
FROM dominio.capa_manzanas cm
JOIN activa a ON a.id = cm.dataset_version_id
WHERE cm.geometria IS NOT NULL
  AND NOT ST_IsValid(cm.geometria)
ORDER BY cm.fila_origen
LIMIT 5;
```

`ST_IsValid` puede emitir mensajes `NOTICE` con las coordenadas de los
defectos. Son evidencia informativa, no errores de ejecución.

### 5.3 H3 — Numeración de manzanos

La columna `cod_man` del sistema conserva el valor del campo fuente
`No_MANZANO` del shapefile, de acuerdo con el perfil
`caranavi-versionado-manzanas`.

```sql
WITH activa AS (
    SELECT id
    FROM dominio.dataset_versiones
    WHERE municipio_codigo = '022001'
      AND estado = 'Activa'
)
SELECT count(*) AS total_manzanas,
       count(*) FILTER (WHERE cm.cod_man IS NULL) AS sin_numero,
       count(cm.cod_man) AS con_numero,
       array_agg(cm.cod_man ORDER BY cm.cod_man)
           FILTER (WHERE cm.cod_man IS NOT NULL) AS valores
FROM dominio.capa_manzanas cm
JOIN activa a ON a.id = cm.dataset_version_id;
```

### 5.4 H4 — Capas definidas para Caranavi

```sql
SELECT tipo_capa, nombre_archivo_shp, obligatoria
FROM dominio.esquemas_capas
WHERE municipio_codigo = '022001'
  AND NOT is_deleted
ORDER BY tipo_capa;
```

### 5.5 H6 — Conteos, geometrías y SRID

```sql
WITH activa AS (
    SELECT id
    FROM dominio.dataset_versiones
    WHERE municipio_codigo = '022001'
      AND estado = 'Activa'
)
SELECT 'manzanas' AS capa,
       count(*) AS registros,
       count(*) FILTER (WHERE geometria IS NOT NULL) AS con_geometria,
       array_agg(DISTINCT ST_SRID(geometria))
           FILTER (WHERE geometria IS NOT NULL) AS srids
FROM dominio.capa_manzanas c
JOIN activa a ON a.id = c.dataset_version_id
UNION ALL
SELECT 'areas_urbanas',
       count(*),
       count(*) FILTER (WHERE geometria IS NOT NULL),
       array_agg(DISTINCT ST_SRID(geometria))
           FILTER (WHERE geometria IS NOT NULL)
FROM dominio.capa_areas_urbanas c
JOIN activa a ON a.id = c.dataset_version_id
UNION ALL
SELECT 'puntos_geodesicos',
       count(*),
       count(*) FILTER (WHERE geometria IS NOT NULL),
       array_agg(DISTINCT ST_SRID(geometria))
           FILTER (WHERE geometria IS NOT NULL)
FROM dominio.capa_puntos_geodesicos c
JOIN activa a ON a.id = c.dataset_version_id;
```

```sql
WITH activa AS (
    SELECT id
    FROM dominio.dataset_versiones
    WHERE municipio_codigo = '022001'
      AND estado = 'Activa'
)
SELECT count(*) AS total,
       count(*) FILTER (
           WHERE nullif(btrim(atributos_extra ->> 'PUNTOS'), '') IS NOT NULL
       ) AS con_identificador,
       count(DISTINCT atributos_extra ->> 'PUNTOS') FILTER (
           WHERE nullif(btrim(atributos_extra ->> 'PUNTOS'), '') IS NOT NULL
       ) AS identificadores_distintos
FROM dominio.capa_puntos_geodesicos c
JOIN activa a ON a.id = c.dataset_version_id;
```

H1 se verifica en el archivo fuente antes del cierre estructural y se encuentra
documentado en ADR 0061. H5 corresponde a la composición del paquete recibido
y a la verificación de proyección realizada durante la importación. Ninguno de
los dos debe sustituirse por una consulta posterior sobre geometrías ya
procesadas.

Las consultas se ejecutaron mediante el wrapper canónico `scripts/sql.ps1`. No
se ejecutó ninguna sentencia de modificación.
