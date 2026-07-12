# ADR 0052 — Tolerancia de geometrías reales en capas versionadas

**Fecha**: 2026-07-12  
**Estado**: Aceptado

## Contexto

Los SHP oficiales de Uyuni contienen registros `Null Shape` en edificaciones
y vías, además de polígonos multipartes en manzanas y zonas. Estos casos son
válidos como evidencia cartográfica de origen, pero el primer cargador exigía
`Polygon` o `LineString` simples y abortaba el paquete completo.

Las parcelas son la unidad jurídica usada para reconciliar el maestro y el
archivo oficial contiene únicamente polígonos simples. Su geometría debe
seguir siendo obligatoria y estricta.

## Decisión

- `capa_parcelas.geometria` permanece `geometry(Polygon,32719) NOT NULL`.
- Las cinco capas poligonales auxiliares usan
  `geometry(MultiPolygon,32719) NULL`.
- `capa_vias.geometria` usa `geometry(MultiLineString,32719) NULL`.
- El lector normaliza los registros SHP `Null Shape`, representados por la
  biblioteca como geometrías vacías, a `null`.
- El cargador normaliza `Polygon` y `LineString` a sus equivalentes Multi de
  una parte, conserva multipartes nativos y carga las filas auxiliares nulas
  junto con sus atributos y `fila_origen`.
- El preview publica la observación no bloqueante `O4` por capa, con conteo,
  fila de origen e identificadores útiles para localizar el objeto en campo.
- Una parcela nula o con `MultiPolygon` de más de una parte sigue causando
  una versión `Fallida` con un mensaje que indica capa, fila, tipo recibido y
  tipo esperado.

La migración M013 aborta si cualquiera de las seis tablas auxiliares contiene
filas. Su reversión también aborta si existen nulos o multipartes, evitando
una reducción silenciosa o pérdida de geometrías.

## Consecuencias

- Los defectos de completitud geométrica quedan preservados y documentados,
  sin fabricar geometrías ni perder atributos del origen.
- La reconciliación no cambia porque consume exclusivamente `capa_parcelas`.
- `ST_IsValid` se evalúa solo para geometrías no nulas; O4 informa las nulas
  por separado.
- Los futuros endpoints MVT de las capas auxiliares deberán filtrar
  `geometria IS NOT NULL` y aceptar `MultiPolygon` o `MultiLineString`.
- M013 no se aplica automáticamente a la base canónica durante este cambio;
  requiere el protocolo operativo de migración e importación autorizado.
