# Dimensionamiento de la fase 3.B: catastro predial de Caranavi desde DGN

**Versión:** 2 — sustituye íntegramente la línea base y las sub-etapas de la
versión del 17 de julio de 2026 (véase «Relación con la versión anterior»).
**Fecha:** 22 de julio de 2026
**Municipio:** Caranavi (`022001`)
**Fuente:** `CARTOGRAFIA_CARANAVI_AGOSTO_2023_V71.dgn` — 122.073 elementos,
capa única `elements`, atributos `Level`/`Type`/`Text`.

## Objetivo

Producir un shapefile de predios de Caranavi a partir de la cartografía DGN
municipal de agosto de 2023. El producto contendrá geometría poligonal, los
componentes de la tripleta distrito-manzano-predio que la evidencia permita
sostener, y los atributos disponibles en la fuente.

El shapefile resultante se importará mediante el pipeline canónico
multi-municipio que ya procesó las capas de manzanas, áreas urbanas y puntos
geodésicos de Caranavi. La fase 3.B es principalmente una labor de preparación
y verificación de datos; no es un desarrollo nuevo del backend.

## Naturaleza real de la fase

El diagnóstico cuantitativo del archivo (cerrado el 22 de julio de 2026, con
comandos reproducibles) estableció que la fase 3.B **no es una importación de
DGN**: es una **reconstrucción de clave catastral con componente de campo y de
gestión**. De los tres componentes de la tripleta canónica
`(cod_uv, cod_man, cod_pred)`:

- **cod_pred** existe en el archivo (Level 6) y cubre el 67,7 % del padrón por
  asociación espacial directa;
- **cod_man** NO es recuperable del archivo por contención: las ~256 etiquetas
  `MZ-*` están dibujadas sobre lotes individuales y no existen polígonos de
  manzana (202 de 210 etiquetas ubicadas caen sobre polígonos < 2.000 m²);
- **cod_uv** (distrito 1–6) NO existe en el archivo. Los rótulos UV-17..21 del
  Level 58 son unidades vecinales, no distritos; mapearlos corrompería la
  clave.

El archivo tampoco contiene altimetría: el Level 10 resultó ser simbología de
relleno (véase alcance por niveles). Cualquier necesidad futura de pendientes
(valuación, drenaje) requerirá DEM externo o levantamiento.

## Fuentes internas del archivo, por prioridad

Línea base verificada sobre `catastral.gpkg` (GeoPackage materializado desde el
DGN, fuente bruta canónica; todo conteo declara su estadio conforme a
AGENTS.md 17-ter).

### Prioridad 1 — Level 4: padrón predial base

6.950 polígonos (Type 6 y Type 14; procesar solo Type 6 perdería el 38 %).
El 95,3 % tiene área entre 50 y 2.000 m² (rango de lote urbano). 33 geometrías
inválidas (0,47 %) a sanear con `ST_MakeValid` antes de cualquier join.

### Prioridad 1 — Level 6: numeración predial (cod_pred)

Textos de numeración. El join espacial por contención dio 4.708 predios
(67,7 %) con exactamente un texto contenido; 1.926 predios sin numeración, de
los cuales ~1.695 tienen área de lote real. Estos ~1.695 predios (24 % del
padrón) constituyen el **componente de campo** de la fase: su numeración no
puede inferirse y no se inventará.

### Prioridad 2 — Levels 34/39/40/42/61: bolsillo complementario

El Level 34 contiene 690 LINESTRING, 10 MULTILINESTRING, 138 POLYGON y 514
POINT. Sus polígonos tienen áreas de 94–7.560 m² (media 411 m²): tamaño lote.
Verificación clave: **cero** de los 138 polígonos tiene su centroide dentro de
un predio del Level 4. No es redundancia — son urbanizaciones dibujadas con
otra convención sobre suelo que el padrón principal no cubre, con un potencial
de ~700–800 lotes adicionales (~10 % del padrón) tras poligonización.

Su ecosistema local: Level 39 (manzanas `MZ`: 128 líneas, 120 textos), Level
40 (numeración predial: 148 líneas, 22 textos), Level 42 (superficies
rotuladas: 48 polígonos, 22 líneas), Level 61 (nombres de vía: 33 líneas, 11
multilíneas).

Se procesa como **sub-etapa diferida** (3.B.5), después del camino troncal.
Si los plazos lo exigen, se difiere a una fase 3.C sin romper el resto.

### Decisión pendiente — Level 55: red vial

Vía cara para derivar `cod_man`: poligonizar la red vial para obtener manzanas
y asignar los rótulos `MZ-*` por proximidad. Solo se abordará si el registro
alfanumérico municipal (véase canal GAM) no llega o llega inutilizable. La
decisión de ejecutarla generará su propio ADR con estimación dedicada.

### Fuera del alcance, con veredicto

- **Level 10** — 71.898 elementos brutos (58,9 % del archivo): 71.864
  LINESTRING, 2 MULTILINESTRING (17 partes), 4 POINT, 28 POLYGON; 0 fuera del
  rango UTM boliviano. Veredicto por estadística y por inspección visual:
  **simbología de relleno** (tramas hexagonales y de retícula para
  clasificación de suelo/cobertura), no curvas de nivel. Segmentos medios de
  ~2,7 m que no comparten extremos; `ST_LineMerge` reduce el conteo solo 17 %.
- **Levels 62 y 63** — concentración de basura de coordenadas según el
  muestreo de la versión anterior de este documento; en todo caso quedan
  cubiertos por el filtro canónico de coordenada absurda.
- **Elementos corruptos** — 2.657 elementos descartables (2,2 % del archivo),
  casi todos colapsados en el origen. Criterio canónico: descarte por
  coordenada fuera del rango UTM 19S boliviano (X 200000–900000,
  Y 7000000–9500000), nunca por bbox municipal.

## Riesgo principal: dos tercios de la tripleta no están en el archivo

El riesgo de la fase no es la viabilidad técnica de la conversión — el padrón
ya está poligonizado en la fuente. El riesgo es estructural:

1. **cod_man y cod_uv no salen del archivo.** Sus vías de recuperación son
   externas (registro municipal) o caras (poligonización vial) o de gestión
   (definición de distritos con el GAM).
2. **El canal con el GAM es informal.** No habrá nota formal de consulta. La
   información municipal llegará, si llega, por vía informal, sin compromiso
   ni fecha. En consecuencia: (a) ninguna ruta crítica del plan depende de una
   entrega del GAM; (b) toda información de origen municipal se registra con
   procedencia informal y se trata como **pista de búsqueda, nunca como fuente
   directa de la tripleta** — los valores que entren por esa vía requieren
   contraste propio antes de usarse.
3. **El 24 % del padrón carece de numeración** y requiere componente de campo
   con presupuesto y cronograma propios (fuera de esta fase; aquí solo se mide
   y reporta).

La mitigación es la misma de la versión anterior, reforzada: medir y reportar
cobertura; nunca inventar números de distrito, manzano o predio. El porcentaje
de predios con tripleta completa —y su desglose por componente faltante— es
simultáneamente el criterio interno de calidad, el gate principal antes de
importar, y el entregable comercial que indicará al GAM qué debe completarse.
Dado el diagnóstico, el escenario esperable es que la primera importación
active el padrón con `cod_pred` mayoritario y `cod_man`/`cod_uv` pendientes o
parciales; el esquema de importación debe tolerar tripleta incompleta sin
inventar valores.

## Sub-etapas y gates de verificación

### 3.B.1 Extracción canónica

Materializar desde el DGN los niveles del alcance (4, 6, 34, 39, 40, 42, 61)
a GeoPackage, aplicando el filtro canónico de coordenada absurda. Cada
artefacto derivado registra el comando exacto que lo generó (AGENTS.md
17-ter); los comandos se versionan en el repositorio al ejecutarse. Las
consultas contra el DGN usan `SELECT *`; el filtrado de columnas ocurre aguas
abajo.

**Gate:** los conteos por nivel y tipo de geometría coinciden con la línea
base de este documento, o toda diferencia queda explicada con su estadio de
procedencia declarado.

### 3.B.2 Saneo del padrón

Aplicar `ST_MakeValid` a las 33 geometrías inválidas del Level 4 y verificar
que ningún join posterior se ejecute sobre geometría inválida (los joins con
`ST_Contains` sobre inválidas producen falsos positivos documentados).

**Gate:** 0 geometrías inválidas en el padrón de trabajo; reporte nominal de
las 33 saneadas y de cualquier área que haya cambiado más de un umbral
razonable al sanear.

### 3.B.3 Asociación de numeración (cod_pred)

Asociar cada texto del Level 6 al predio del Level 4 mediante contención del
**centroide** del texto (`ST_PointOnSurface` para casos cóncavos), conforme al
criterio canonizado. No completar ni inferir valores ausentes.

**Gate y hallazgo:** reproducir y congelar los cuatro grupos: predios con
número (línea base: 4.708), predios sin número (1.926, de ellos ~1.695 con
área de lote), números huérfanos, y asociaciones ambiguas (más de un texto por
predio).

### 3.B.4 Tripleta parcial y reporte de cobertura

Construir la tripleta con lo que la evidencia sostenga:

- **cod_pred** desde 3.B.3;
- **cod_man**: solo si para entonces existe registro municipal contrastado o
  se aprobó y ejecutó la vía del Level 55; en su defecto queda vacío;
- **cod_uv**: solo si el GAM define la delimitación distrital; en su defecto
  queda vacío.

**Gate:** reporte de cobertura por componente y por combinación (solo
cod_pred; cod_pred+cod_man; tripleta completa), con conteos absolutos. Este
reporte es el número comercial central de la fase.

### 3.B.5 Anexo Level 34 (diferible)

Poligonizar los contornos del Level 34 (cerrar las líneas abiertas agregando
el punto de cierre sin mover coordenadas existentes, mismo principio del ADR
0061), asociar la numeración local (Level 40) y las manzanas locales (Level
39) por centroide, y fusionar al padrón sin colisionar con el Level 4.

**Gate de entrada:** inspección visual en QGIS del Level 34 contra el Level 4
(deuda declarada de este dimensionamiento). **Gate de salida:** conteo de
polígonos coherente con los contornos, reporte de huérfanos, y verificación de
no-solape con el padrón base (centroides, línea base: 0 solapes).

### 3.B.6 Shapefile de predios

Generar el shapefile con los polígonos, los componentes de tripleta
disponibles y los atributos asociables sin ambigüedad (superficie rotulada del
Level 42 donde aplique). Incluir `.prj` (EPSG:32719) y respetar el formato de
campos del perfil de importación de Caranavi.

**Gate:** paquete completo y legible en QGIS, con geometrías, CRS, campos y
conteos contrastados contra el resultado aprobado de 3.B.4 (y 3.B.5 si se
ejecutó).

### 3.B.7 Importación canónica

Registrar `TipoCapa.Predios` en el esquema municipal de Caranavi, subir el
paquete por el pipeline multi-municipio existente, revisar el preview y
activar la versión solamente después de superar sus controles. El visor
habilitará búsqueda y ficha predial automáticamente (ADR 0062).

**Gate:** preview sin bloqueantes, conteos conciliados, cobertura de tripleta
visible en la evidencia de entrega y versión activada por el flujo de dominio.

## No-objetivos

Quedan expresamente fuera del alcance de la fase 3.B:

- el levantamiento de campo de los ~1.695 predios sin numeración (se mide y
  reporta; su ejecución es un proyecto con presupuesto propio);
- la poligonización de la red vial del Level 55 (decisión pendiente con ADR
  propio si se aprueba);
- las zonas de valuación como capa independiente y la valuación de los
  predios;
- cualquier fuente de altimetría (el archivo no la contiene);
- cualquier cambio en el backend, salvo el registro del esquema predial de
  Caranavi previsto en 3.B.7.

La zona catastral se conservará como atributo cuando esté disponible, como
insumo de valuación futura.

## Naturaleza iterativa

La fase 3.B no es una tubería de un solo intento. Cada sub-etapa produce
evidencia verificable antes de avanzar; las diferencias, faltantes y
excedentes se determinan observando el resultado espacial real, no mediante
supuestos sobre la estructura del DGN — estructura que, como quedó demostrado,
se ensambló por urbanizaciones con convenciones distintas y sin mapeo estable
nivel→semántica.

## Herramientas y entorno de ejecución

La conversión y preparación se ejecutan con GDAL (OSGeo4W), PostGIS y QGIS en
la máquina del orquestador. Codex no dispone de GDAL y no procesa el DGN
directamente. El backend nunca procesará archivos DGN: recibirá el shapefile
preparado mediante el contrato de importación existente.

## Relación con la versión anterior y con la fase 3.A

La versión del 17 de julio se construyó sobre un muestreo del rectángulo
`640000-665000 / 8230000-8255000` que capturó el bolsillo de urbanizaciones
del Level 34 (sus conteos coinciden exactamente: 690/138/514 en L34, ~150/22
en L40, ~130/122 en L39) y lo generalizó como línea base del archivo completo.
El diagnóstico posterior demostró que el padrón principal vive en el Level 4
(6.950 polígonos) y la numeración en el Level 6. La afirmación «el GAM
confirmó que la cartografía contiene distrito y zona» quedó refutada por
evidencia: no hay polígonos de distrito ni de manzana en el archivo. Esta v2
conserva de la versión anterior los principios (nunca inventar numeración,
cobertura como gate y entregable, iteración con evidencia) y la sub-etapa de
importación canónica; sustituye la línea base, el mapeo de niveles y las
sub-etapas de preparación.

La fase 3.B continúa el trabajo multi-municipio cerrado en 3.A y se apoya en
los ADR 0058 a 0062. Este documento es un dimensionamiento de trabajo, no un
ADR. Cada sub-etapa que establezca una decisión de diseño firme generará su
propio ADR durante la ejecución.
