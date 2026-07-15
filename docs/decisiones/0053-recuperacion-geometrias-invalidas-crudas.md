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

## Fe de erratas — 2026-07-15

La explicación anterior de `actualizadas=11985, sinCambio=0` como un simple
re-apuntado es incorrecta. El contador `actualizadas` aumenta solamente cuando
`Predio.ReconciliarDesdeDataset` devuelve `true`, es decir, cuando alguna de
las condiciones de `cambioImportado` lleva al maestro por el camino de UPDATE.
`UltimaVersionVistaId` se modifica como consecuencia de ese UPDATE; no es por
sí solo una condición para generarlo.

En la activación de v3, las 11.985 filas compararon una `superficie_sig`
persistida como `numeric(14,4)` contra el área recién calculada sin redondear.
La comparación decimal cruda resultó distinta en las 11.985, aunque a cuatro
decimales hubo cero diferencias. Adicionalmente, 391 parcelas presentaron una
geometría topológicamente distinta entre v2 y v3. Por tanto, la igualdad de
atributos v2/v3 no demostraba que el dominio hubiera tomado el camino
`sinCambio` ni que el único efecto fuese rotar el identificador de versión.

La semántica demostrada de `UltimaVersionVistaId` es: última versión cuya
reconciliación produjo un cambio de contenido según las comparaciones del
dominio. No equivale necesariamente a la versión activa. La vigencia de un
predio se determina por el triplete único, `presente_en_version_activa` y
`NOT is_deleted`; la versión cartográfica se resuelve independientemente por
`dataset_versiones.estado = 'Activa'`.
