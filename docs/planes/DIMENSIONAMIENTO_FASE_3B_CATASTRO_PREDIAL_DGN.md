# Dimensionamiento de la fase 3.B: catastro predial de Caranavi desde DGN

**Fecha:** 17 de julio de 2026
**Municipio:** Caranavi (`022001`)
**Fuente:** `CARTOGRAFIA_CARANAVI_AGOSTO_2023_V71.dgn`

## Objetivo

Producir un shapefile de predios de Caranavi a partir de la cartografía DGN
municipal de agosto de 2023. El producto contendrá geometría poligonal, la
tripleta distrito-manzano-predio y los atributos disponibles en la fuente.

El shapefile resultante se importará mediante el pipeline canónico
multi-municipio que ya procesó las capas de manzanas, áreas urbanas y puntos
geodésicos de Caranavi. La fase 3.B es principalmente una labor de preparación
y verificación de datos; no es un desarrollo nuevo del backend.

## No-objetivos

Quedan expresamente fuera del alcance de la fase 3.B:

- las construcciones del nivel DGN 4, que comprenden aproximadamente 6.871
  polígonos;
- las zonas de valuación como capa independiente;
- los poblados aledaños situados fuera del casco urbano;
- la valuación de los predios;
- cualquier cambio en el backend, salvo el registro del esquema predial de
  Caranavi previsto en 3.B.6.

La zona catastral se conservará como atributo cuando esté disponible, para
servir como insumo de una valuación futura. Su captura no implica implementar
ni ejecutar valuación en esta fase.

## Línea base medida

La viabilidad fue verificada por el orquestador con GDAL sobre el DGN, aplicando
el rectángulo de interés `640000-665000 / 8230000-8255000` en `EPSG:32719`.
Esta medición constituye la línea base para los gates de exportación:

| Nivel DGN | Contenido observado | Línea base |
|---|---|---:|
| 34 | Contornos de lotes | 690 líneas; 624 cerradas; 138 polígonos ya formados; 514 puntos de texto |
| 40 | Numeración de predio | Aproximadamente 150 líneas y 22 puntos de texto |
| 39 | Numeración de manzano (`MZ` + número) | Aproximadamente 130 líneas y 122 puntos de texto |
| 42 | Superficies rotuladas | Presente |
| 61 | Nombres de vía | Presente |

El GAM confirmó que la cartografía contiene distrito y zona. Sus niveles DGN
exactos se identificarán mediante muestreo durante 3.B.1.

La basura de coordenadas está concentrada en los niveles 62 y 63, que se
excluirán. Unos pocos elementos sueltos de otros niveles también quedan fuera
al aplicar el rectángulo de interés.

## Riesgo principal: completitud de la numeración

La numeración predial observada en el DGN es escasa en relación con la cantidad
de lotes: existen cientos de textos para cientos de lotes, sin garantía de una
correspondencia de uno a uno. Por tanto, el riesgo principal de 3.B no es la
viabilidad técnica de convertir o poligonizar la cartografía, sino la
completitud del dato catastral.

Es previsible que una fracción de los lotes no alcance una tripleta completa.
La mitigación consiste en medir y reportar esa cobertura; nunca se inventarán
números de distrito, manzano o predio. El porcentaje de lotes con tripleta
completa será simultáneamente:

- el criterio interno de calidad de la preparación;
- el gate principal antes de importar; y
- el entregable comercial que indicará al GAM qué numeración debe completar
  en la fuente.

## Sub-etapas y gates de verificación

### 3.B.1 Exportación selectiva

Convertir el DGN a GeoPackage o PostGIS, filtrando simultáneamente por nivel
catastral y por el rectángulo de interés. La primera tarea será identificar por
muestreo los niveles que contienen distrito y zona.

**Gate:** los conteos por nivel coinciden con la línea base medida o toda
diferencia queda explicada y documentada.

### 3.B.2 Poligonización

Ejecutar `ST_Polygonize` sobre los contornos de lote del nivel 34. Antes de
poligonizar, cerrar las aproximadamente 66 líneas abiertas agregando el punto
de cierre sin mover ni sustituir coordenadas existentes. Este tratamiento
sigue el mismo principio aplicado por M-LECTOR-2 y documentado en el ADR 0061.

**Gate:** el número de polígonos obtenidos es coherente con los contornos
observados y se entrega un reporte nominal de las líneas huérfanas que no
lograron formar polígono.

### 3.B.3 Asociación espacial de numeración

Asociar cada texto representado como punto al polígono de lote que lo contiene,
mediante `ST_Contains`. No completar ni inferir valores ausentes.

**Gate y hallazgo:** medir y reportar cuatro grupos: lotes con número, lotes
sin número, números huérfanos que no caen en ningún lote y asociaciones
ambiguas.

### 3.B.4 Construcción de la tripleta y reporte de cobertura

Construir la tripleta con:

- distrito, obtenido por contención en el polígono de distrito;
- manzano, obtenido del nivel 39; y
- predio, obtenido del nivel 40.

La tripleta `(distrito, manzano, predio)` se formará solamente cuando existan
los tres componentes.

**Gate:** calcular y reportar el porcentaje de lotes con tripleta completa. Ese
porcentaje es el número comercial central de la fase y debe acompañarse con los
conteos absolutos de casos completos e incompletos.

### 3.B.5 Shapefile de predios

Generar el shapefile de predios con los polígonos, la tripleta y los atributos
disponibles, entre ellos la superficie rotulada del nivel 42 cuando pueda
asociarse sin ambigüedad. Incluir el archivo `.prj` y respetar el formato de
campos que espera el perfil de importación de Caranavi.

**Gate:** paquete completo y legible en QGIS, con geometrías, CRS, campos y
conteos contrastados contra el resultado aprobado de 3.B.4.

### 3.B.6 Importación canónica

Registrar `TipoCapa.Predios` en el esquema municipal de Caranavi, subir el
paquete por el pipeline multi-municipio existente, revisar el preview y activar
la versión solamente después de superar sus controles.

El visor habilitará automáticamente la búsqueda y la ficha predial para Caranavi
porque esas capacidades ya se resuelven a partir de las capas presentes, según
el ADR 0062.

**Gate:** preview sin bloqueantes, conteos conciliados, cobertura de tripleta
visible en la evidencia de entrega y versión activada por el flujo de dominio.

## Naturaleza iterativa

La fase 3.B no es una tubería de un solo intento. La cartografía se
poligonizará, se inspeccionará visualmente en QGIS y se ajustará a partir del
resultado real. El presente dimensionamiento fija el rumbo, los límites y los
gates, pero no pretende anticipar todos los detalles que emergerán durante la
ejecución.

Cada sub-etapa producirá evidencia verificable antes de avanzar. Las diferencias,
faltantes y excedentes se determinarán observando el resultado espacial real,
no mediante supuestos sobre la estructura del DGN.

## Herramientas y entorno de ejecución

La conversión y preparación se ejecutarán con GDAL, PostGIS y QGIS en la
máquina del orquestador. Codex no dispone de GDAL en su entorno y no procesará
el archivo DGN directamente.

Los comandos canónicos de conversión se versionarán en el repositorio al
ejecutar 3.B.1, una vez verificados sobre la fuente real. El backend nunca
procesará archivos DGN: recibirá el shapefile preparado mediante el contrato de
importación ya existente.

## Relación con la fase 3.A y decisiones futuras

La fase 3.B continúa el trabajo multi-municipio cerrado en 3.A y se apoya en
los ADR 0058 a 0062. Este documento es un dimensionamiento de trabajo, no un
ADR. Cada sub-etapa que establezca una decisión de diseño firme generará su
propio ADR durante la ejecución.
