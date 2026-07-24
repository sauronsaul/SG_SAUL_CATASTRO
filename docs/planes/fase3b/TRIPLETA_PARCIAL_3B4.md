# Fase 3.B.4 — Tripleta parcial y reporte de cobertura de Caranavi

**Fecha de ejecución:** 24 de julio de 2026
**Ejecutor:** orquestador (PostGIS del contenedor sg_postgres)
**Entradas:** `fase3b_tmp.padron_final` (6.573) y `fase3b_tmp.asignacion`
(3.B.3)
**Salida (evidencia viva):** `fase3b_tmp.tripleta_parcial` — un registro por
predio del padrón final con cod_pred, sufijo, cod_man (NULL), cod_uv (NULL),
requiere_revision y estado

## Resultado del gate: SUPERADO

Reporte de cobertura del padrón final (6.573 predios únicos):

| Combinación | predios | % |
|---|---:|---:|
| Solo cod_pred | 4.486 | 68,2 % |
| cod_pred + cod_man | 0 | 0 % |
| Tripleta completa (cod_uv+cod_man+cod_pred) | 0 | 0 % |
| Sin ningún componente | 2.087 | 31,8 % |

Desglose de los 4.486 con cod_pred, por forma del texto de origen:

| Estado | predios | lleva sufijo |
|---|---:|:--:|
| asignado_puro (p. ej. "12") | 3.759 | no |
| asignado_sufijo_letra ("1a", "4b") | 539 | sí |
| asignado_compuesto ("1.2", "1-A") | 140 | sí |
| asignado_otro (numérico con forma atípica) | 48 | sí |

Desglose de los 2.087 sin cod_pred:

| Estado | predios |
|---|---:|
| sin_texto (componente de campo) | 1.843 |
| ambiguo_2mas_textos (revisión) | 226 |
| texto_no_numerico ("AREA", "Capacitación") | 18 |

Control aritmético: 3.759+539+140+48 = 4.486 con cod_pred;
4.486+1.843+226+18 = 6.573.

## Número comercial de la fase

**4.486 de 6.573 predios (68,2 %) con cod_pred recuperado por evidencia
espacial.** cod_man y cod_uv: 0 % — no existen en el archivo (ver
dimensionamiento v2). La primera importación activará el padrón con cod_pred
mayoritario y los otros dos componentes vacíos, vía capa
predios_no_fotografiados que tolera tripleta incompleta.

## Regla de extracción de cod_pred

cod_pred = prefijo numérico inicial del texto (`^([0-9]+)`); el resto del
texto se conserva literal en el campo `sufijo`. Un texto que no empieza por
dígito no recibe cod_pred. Fundamento: en la cartografía de Caranavi,
ensamblada por urbanizaciones sin convención única, la subdivisión de lote
se escribe de formas equivalentes por distintos dibujantes — "1a", "1-a",
"1.A" son el mismo concepto (lote 1, subdivisión A). El prefijo numérico es
el lote; el sufijo es el discriminante de subdivisión, preservado sin
interpretarse. Nunca se inventa numeración: lo que no empieza por dígito
queda sin cod_pred.

Nota de revisión pendiente: los 140 predios "asignado_compuesto" (textos con
punto/coma/guion tras el número, p. ej. "1.2") se leyeron como subdivisión
(prefijo=lote). La hipótesis alternativa —que el separador codifique
manzana-lote— fue considerada y descartada por la evidencia: los sufijos son
correlativos bajos (1.1, 1.2, 1.3; 6.1, 6.2, 6.3), patrón de subdivisión, no
de pares manzana-lote dispares. Quedan con estado propio para reclasificación
trivial si el trabajo de campo lo desmiente.

## Hallazgo para el ADR de identidad provisional

La colisión de cod_pred cuantifica por qué el padrón no puede ingresar al
registro maestro con el índice único de triplete mientras cod_man/cod_uv
sean nulos:

- Valores distintos de cod_pred: 96
- Valores de cod_pred repetidos (compartidos por 2+ predios): 82
- Colisión máxima: 478 predios comparten un mismo cod_pred (el valor "1")

Con solo 96 números distintos para 4.486 predios, la unicidad depende
enteramente de cod_man y cod_uv. Esta es la evidencia central del ADR de
identidad provisional diferido (ver adenda del dimensionamiento v2): la
importación al maestro se posterga hasta que exista una vía real para los
componentes faltantes; entretanto, la capa predios_no_fotografiados —de
tripleta anulable— es el vehículo correcto.

## Secuencia ejecutada

Materialización de la tripleta parcial (PowerShell, un registro por predio;
cod_pred por prefijo numérico; ambiguos y sin-texto con estado explícito;
cod_man y cod_uv NULL):

    powershell -ExecutionPolicy Bypass -File scripts\sql.ps1 -Sql "CREATE TABLE fase3b_tmp.tripleta_parcial AS WITH un_texto AS (SELECT fid_predio FROM fase3b_tmp.asignacion WHERE fid_predio IS NOT NULL GROUP BY fid_predio HAVING COUNT(*) = 1), ambiguos AS (SELECT fid_predio FROM fase3b_tmp.asignacion WHERE fid_predio IS NOT NULL GROUP BY fid_predio HAVING COUNT(*) > 1) SELECT p.fid_gpkg AS fid_predio, ST_Area(p.geom) AS area_m2, CASE WHEN a.texto ~ '^[0-9]+' THEN (regexp_match(a.texto, '^([0-9]+)'))[1]::int ELSE NULL END AS cod_pred, CASE WHEN a.texto ~ '^[0-9]+' THEN NULLIF(regexp_replace(a.texto, '^[0-9]+', ''), '') ELSE a.texto END AS sufijo, NULL::int AS cod_man, NULL::int AS cod_uv, (amb.fid_predio IS NOT NULL) AS requiere_revision, CASE WHEN amb.fid_predio IS NOT NULL THEN 'ambiguo_2mas_textos' WHEN ut.fid_predio IS NULL THEN 'sin_texto' WHEN a.texto ~ '^[0-9]+$' THEN 'asignado_puro' WHEN a.texto ~ '^[0-9]+[a-zA-Z]$' THEN 'asignado_sufijo_letra' WHEN a.texto ~ '^[0-9]+[.,-]' THEN 'asignado_compuesto' WHEN a.texto ~ '^[0-9]+' THEN 'asignado_otro' ELSE 'texto_no_numerico' END AS estado FROM fase3b_tmp.padron_final p LEFT JOIN un_texto ut ON ut.fid_predio = p.fid_gpkg LEFT JOIN ambiguos amb ON amb.fid_predio = p.fid_gpkg LEFT JOIN fase3b_tmp.asignacion a ON a.fid_predio = p.fid_gpkg AND ut.fid_predio IS NOT NULL;"

Los reportes de cobertura, desglose por estado y colisión de cod_pred se
obtuvieron con GROUP BY sobre esta tabla (valores en las tablas de arriba).

## Estado de la fase tras 3.B.4

Componentes de la tripleta para la primera entrega de Caranavi:
- cod_pred: 4.486 / 6.573 (68,2 %) — asignado
- cod_man: 0 — diferido (vía Level 55 o registro municipal informal)
- cod_uv: 0 — diferido (gestión de distritos con el GAM)

Siguiente: 3.B.6 (generar PRE_NOF_CAR.shp desde padron_final + tripleta_parcial
conforme al contrato del seed) y 3.B.7 (importación por el pipeline).
