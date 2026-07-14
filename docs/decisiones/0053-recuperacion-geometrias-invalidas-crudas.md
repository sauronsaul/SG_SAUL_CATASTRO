# ADR 0053 — Recuperación de geometrías inválidas crudas

**Fecha**: 2026-07-14  
**Estado**: Aceptado

## Contexto

La importación de la entrega oficial de Uyuni mostraba 32 registros que el
lector estricto de NetTopologySuite no lograba construir: 30 edificaciones y 2
manzanas. En la versión 2 esos registros quedaban con geometría nula y se
mezclaban con los 28 nulos genuinos del origen bajo la observación O4.

La geometría inválida y la geometría ausente son defectos distintos. La primera
conserva evidencia espacial útil, mientras que la segunda no contiene
coordenadas que puedan recuperarse. Corregir topología automáticamente también
alteraría el documento cartográfico recibido y ocultaría un defecto que debe
ser devuelto al GAM.

## Decisión

- O1 identifica geometrías topológicamente inválidas que sí quedaron
  persistidas. Se conserva la geometría cruda y nunca se repara en silencio.
- O4 identifica exclusivamente geometrías nulas genuinas. Un `Null Shape`
  devuelto como geometría vacía por el lector estricto se normaliza a `null` y
  jamás entra al camino de recuperación.
- El lector SHP estricto y el lector alternativo con
  `GeometryBuilderMode.IgnoreInvalidShapes` avanzan en lockstep. El alternativo
  solo aporta su geometría cuando el lector estricto falla al construir un
  registro que contiene bytes de geometría.
- Los errores de stream o archivo roto continúan siendo fallos no recuperables;
  el fallback no los convierte en observaciones de calidad.
- El preview evalúa únicamente geometrías no nulas con `ST_IsValid` y persiste
  en `GeometriasInvalidas` la razón emitida por `ST_IsValidReason`/`IsValidOp`,
  sin normalizar ni sustituir su texto.

Las razones reales persistidas en la versión 3 incluyen:

- `Self-intersection[...]`.
- `Ring Self-intersection[...]`.
- `Too few points in geometry component[...]`.

## Evidencia de cierre

La versión 3 quedó activa con 35.013 objetos, los mismos conteos por capa que la
versión 2. El gate de persistencia confirmó 32 O1 —30 edificaciones y 2
manzanas—, todos con geometría no nula y razón registrada; 28 O4 —4
edificaciones y 24 vías—; O2 y O3 en cero; cero bloqueantes; y texto idéntico
entre versiones.

La comparación exhaustiva por `(capa, fila_origen)`, excluyendo únicamente los
identificadores técnicos de fila/versión y la geometría, produjo 35.013 pares,
cero filas exclusivas de una versión, cero atributos distintos y 35.013
atributos iguales.

El resumen de reconciliación `actualizadas=11985, sinCambio=0` refleja el
re-apuntado de los predios maestros a las filas de la versión nueva conforme a
las reglas R-1..R-4; no significa que hayan cambiado sus contenidos. La
igualdad de atributos entre v2 y v3 está probada sobre los 35.013 pares.

## Consecuencias

- La evidencia cartográfica de origen se conserva sin fabricar correcciones.
- O1 y O4 mantienen semánticas separadas y conteos auditables.
- Las razones de invalidez pueden variar según `IsValidOp`; el sistema conserva
  literalmente el diagnóstico producido para cada fila.
- La sincronización DBF/SHP sigue protegida por el conteo previo y por la
  verificación de avance de ambos lectores en cada registro.
- La reconciliación de predios mantiene las reglas R-1..R-4 y no interpreta el
  re-apuntado de versión como una diferencia de atributos.
