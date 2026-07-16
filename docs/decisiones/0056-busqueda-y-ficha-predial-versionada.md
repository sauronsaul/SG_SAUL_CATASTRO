# ADR 0056 - Búsqueda y ficha predial sobre la versión activa

**Fecha**: 2026-07-15
**Estado**: Aceptado
**Relación**: implementa T-2.3 y T-2.4; complementa los ADR 0053, 0054 y
0055.

## Contexto

El visor institucional necesita localizar un predio por el triplete canónico
`(cod_uv, cod_man, cod_pred)`, encuadrar su geometría y abrir una ficha con
datos alfanuméricos provenientes de la versión cartográfica activa. Los tiles
MVT ya transportan el triplete para identificar una parcela al hacer clic,
pero no deben cargar todos los atributos de la ficha.

La implementación y la verificación de doble activación expusieron que
`ultima_version_vista_id` no es un puntero general a la versión activa. También
se caracterizaron atributos del SHP que hasta ahora carecen de diccionario
formal del GAM.

## Decisión

- La API expone `GET /api/predios/buscar?distrito={d}&manzana={m}&predio={p}`
  para los roles `Admin` y `Tecnico`. Los tres componentes deben ser mayores o
  iguales a uno; no se codifica en el sistema el rango observado de distritos
  de Uyuni.
- La infraestructura resuelve en cada consulta la versión con
  `dataset_versiones.estado = 'Activa'` para el municipio configurado. La
  búsqueda une `capa_parcelas` y `predios` por triplete.
- El predicado de vigencia del maestro es el triplete único más
  `presente_en_version_activa` y `NOT is_deleted`. No se exige
  `ultima_version_vista_id = dataset_version_id`.
- El visor usa el triplete incluido en las propiedades MVT para solicitar la
  ficha completa. La búsqueda explícita encuadra el bbox calculado por PostGIS;
  el clic abre el mismo panel lateral sin introducir un segundo contrato.
- Búsqueda y clic activan las mismas capas de resaltado predefinidas sobre la
  fuente `parcelas`, filtradas por triplete. El filtro sobrevive la recarga de
  tiles al cambiar de zoom y se limpia al cerrar la ficha, sin endpoint nuevo
  ni dependencia de la estabilidad de `feature.id`.
- La ficha muestra versión interna del dataset, fila de origen, códigos,
  ubicación, atributos de uso, servicios y superficies. No edita datos.

## Semántica de `ultima_version_vista_id`

`ultima_version_vista_id` identifica la última versión cuya reconciliación
produjo un cambio de contenido según `Predio.ReconciliarDesdeDataset`. No
equivale necesariamente a la versión cartográfica activa: cuando la
reconciliación devuelve `false`, el dominio evita un UPDATE y no rota ese
identificador.

En Uyuni v3 los 11.985 punteros terminaron en v3 porque las 11.985 superficies
SIG persistidas a cuatro decimales se compararon con áreas recién calculadas a
precisión cruda y el dominio clasificó las filas como actualizadas. A cuatro
decimales hubo cero diferencias. La fe de erratas correspondiente queda en
ADR 0053.

## Interpretación de superficies

La columna denominada `superficie` en el SHP coincide con `ST_Area` de la
geometría al cuarto decimal en los 11.985 pares de la versión activa. Es un
área SIG entregada por el GAM, aunque el contrato de la ficha la denomina
`SuperficieDeclaradaM2` para distinguirla de la superficie recalculada y de
una eventual superficie oficial.

La coincidencia actual no es un error del visor ni prueba una declaración del
contribuyente. El contraste cobrará valor cuando el sistema incorpore
declaraciones u otras fuentes oficiales en fases posteriores.

## Dato personal y DP-04

`PropietarioReferencia` se muestra porque la ficha pertenece al sistema
institucional autenticado. Es un dato personal y constituye entrada explícita
para DP-04: antes de habilitar el portal ciudadano debe decidirse qué atributos
son públicos, cuáles requieren anonimización y cuáles permanecen internos.

## Efecto medido de M-LECTOR-1

Entre capa_parcelas v2 y v3 existen 391 pares para los que `ST_Equals` es
falso. La distribución de
`ST_Area(ST_SymDifference(geometria_v2, geometria_v3))`, en metros cuadrados,
es:

| Métrica | Resultado |
|---|---:|
| Máximo | `2.8764901072013144e-08 m²` |
| Mediana | `8.180970327174505e-10 m²` |
| Percentil 95 | `7.502367954691832e-09 m²` |
| Diferencias mayores a `1 m²` | `0` |

Son diferencias numéricas submétricas del camino de lectura, sin impacto
material en superficie. Se registran como caracterización del efecto de
M-LECTOR-1; no justifican corrección silenciosa de geometrías ni cambio del
informe al GAM.

## Diccionario provisional de atributos del GAM

En las 11.985 parcelas de Uyuni v3:

- `tipo_inmueble` y `uso_terreno` comparten exactamente el mismo catálogo de
  15 códigos de tres letras, sin diferencias de conjunto: `CMC`, `COM`, `CUL`,
  `DEP`, `EDU`, `IND`, `OFI`, `REC`, `REL`, `SAL`, `SER`, `SIN`, `TRR`, `TRU`
  y `VIV`. `TRU` aparece 21 veces en tipo y 35 en uso.
- `topografia_terreno` contiene `PLA`, `SPL` e `INC`, además de vacíos.
- Los cuatro campos de servicios contienen únicamente `SI`, `NO` o vacío.

La consistencia, la reutilización del catálogo y el modelo de tres caracteres
de `uso_terreno` no muestran palabras cortadas a mitad durante la importación.
Sin embargo, el repositorio no contiene las expansiones oficiales de esos
códigos. Se preservan como códigos opacos y se solicita al GAM el diccionario;
el sistema no inventa significados ni reescribe los datos.

## Texto libre de barrio y vía

La verificación posterior del croquis confirmó que `direccion_barrio` y
`nombre_via` conservan texto libre del origen. La versión activa incluye
variaciones de mayúsculas, tildes y grafías que no pueden resolverse sin
autoridad institucional; los conteos crudos quedan en
`INFORME_CALIDAD_DATOS_UYUNI_V2_HALLAZGOS.md`.

La futura fase de edición debe diseñar catálogos controlados de barrios y vías
con identificador estable, denominación oficial, alias e historial. Esta nota
no autoriza normalización silenciosa de los valores importados ni una migración
automática desde texto libre.

`codigo_geografico` vale exactamente `04-12-05-01` en las 11.985 parcelas y
cumple el formato `NN-NN-NN-NN`. Por ser constante, no identifica parcela,
manzana o distrito y no forma parte del triplete canónico. Tampoco puede
afirmarse que sea el código INE de Uyuni: el clasificador público del INE usa
`051201` para Potosí - Antonio Quijarro - Uyuni. Hasta recibir el diccionario
del GAM, `04-12-05-01` se documenta como código geográfico institucional de
significado no demostrado, separado del triplete.

Fuente de contraste: [catálogo geográfico público del INE para
Uyuni](https://anda.ine.gob.bo/index.php/catalog/28/variable/F5/V1418?name=Ms01_0103_2_1cod).

## Consecuencias

- Una nueva activación cambia la versión y la ficha cartográfica sin exigir que
  el puntero histórico del maestro coincida con ella.
- Búsqueda y clic reutilizan un único endpoint y un único DTO compartido.
- `fila_origen` sigue siendo el `feature.id` MVT para trazabilidad; el clic usa
  las propiedades del triplete y no depende de ese identificador.
- Las superficies y códigos del SHP se muestran sin reinterpretación. La
  semántica institucional pendiente queda visible como deuda de diccionario,
  no como transformación de datos.
- El alcance público de `PropietarioReferencia` queda bloqueado por DP-04.
