# ADR 0064 — Estatus de la capa predial de Caranavi: carril `PrediosNoFotografiados` como estado transitorio

**Fecha**: 2026-07-26
**Estado**: Aceptado
**Autores**: Saul Gutierrez + equipo del proyecto
**Relacionado**: ADR 0049, ADR 0060, ADR 0063, ADR 0051

---

## Contexto

El esquema municipal de Caranavi (`022001`) declara la capa predial generada en
fase 3.B como `TipoCapa.PrediosNoFotografiados`, con destino
`capa_predios_no_fotografiados` y `obligatoria = false`. **No declara
`TipoCapa.Predios`.**

Esta elección no fue arbitraria. El carril `Predios` impone dos requisitos que
los datos actuales de Caranavi no satisfacen:

1. **Clave obligatoria en la carga.** `CargaVersionadaServicio.CrearParcela`
   obtiene `cod_uv`, `cod_man` y `cod_pred` mediante `AEntero`, que lanza
   excepción ante valor ausente o no convertible. El padrón de Caranavi tiene
   6.573 predios únicos y una fracción sin `cod_pred` (ver *Cifras pendientes*).
   Cualquier fila sin `cod_pred` abortaría la carga completa.
2. **Unicidad del triplete en la reconciliación.**
   `ActivacionVersionServicio.ReconciliarAsync` lanza
   `Triplete duplicado durante reconciliación` ante la primera colisión de
   `(cod_uv, cod_man, cod_pred)`. La colisión máxima medida en 3.B.4 fue de 478
   registros sobre un mismo triplete.

El carril `PrediosNoFotografiados` usa `AEnteroOpcional` para las tres
componentes del triplete y no participa en la reconciliación, por lo que admite
claves incompletas y colisionantes.

## Decisión

Se acepta `PrediosNoFotografiados` como **carril transitorio** para la capa
predial de Caranavi, con alcance explícitamente delimitado, y se fijan las
condiciones cuantificadas para su promoción futura a `TipoCapa.Predios`.

### Alcance que este carril SÍ entrega (verificado en código)

- Renderizado en el visor: `TileVectorialService` mapea el tipo a
  `capa_predios_no_fotografiados` y expone `cod_uv, cod_man, cod_pred` como
  atributos del tile vectorial.
- Presentación configurada: `CatalogoPresentacionCapasVisor` declara la capa
  `predios-no-fotografiados` con estilo propio.
- Contribución a la extensión geográfica municipal vía
  `ExtensionMunicipalService`.
- Conteos y validación en el reporte de preview
  (`ReportePreviewVersionServicio`).

### Alcance que este carril NO entrega (verificado en código y en datos)

- **Ningún registro en `dominio.predios`.** `ActivacionVersionServicio` calcula
  `tienePredios = esquemaMunicipal.Any(x => x.TipoCapa == TipoCapa.Predios)`;
  para Caranavi es falso, y la reconciliación se omite registrando el motivo en
  `ResumenReconciliacion`. Consulta del 26 de julio de 2026: `dominio.predios`
  contiene 11.985 filas de Uyuni (`051201`) y **cero** de Caranavi.
- **Ninguna búsqueda por triplete ni ficha predial.**
  `ConsultaPredioVersionado` une exclusivamente `dominio.capa_parcelas` con
  `dominio.predios`; no consulta `capa_predios_no_fotografiados` en ninguna
  ruta.
- **Ninguna base para valuación**, que depende del maestro predial.

Esta delimitación debe reflejarse en toda comunicación con el GAM Caranavi: lo
que el sistema muestra es cartografía predial visualizable, no un registro
catastral consultable.

### Condiciones para promover a `TipoCapa.Predios`

La promoción requiere que se cumplan **todas**:

1. Cobertura de `cod_pred` del 100 % sobre las filas a importar. No admite
   valores nulos ni no convertibles a entero.
2. Cero colisiones del triplete `(cod_uv, cod_man, cod_pred)` dentro del
   conjunto a importar.
3. `superficie` presente y estrictamente positiva en toda fila
   (`ReconciliarAsync` lanza ante ausencia o valor no positivo).
4. Geometría convertible a `Polygon`, admitiendo `MultiPolygon` de una sola
   parte (`PoligonoParcela`). Un `MultiPolygon` de dos o más partes aborta la
   carga.
5. Alta de `TipoCapa.Predios` en `dominio.esquemas_capas` para `022001` con su
   perfil de mapeo, y decisión documentada sobre qué ocurre con las filas ya
   cargadas en `capa_predios_no_fotografiados`.

Mientras no se cumplan, la capa permanece en el carril transitorio.

## Justificación

Reconocer el límite es preferible a forzar el carril `Predios` con datos que lo
harían fallar en carga o en activación. El entregable parcial —primera
cartografía predial de Caranavi visible en el sistema— tiene valor demostrable
por sí mismo y no compromete el maestro predial.

Las condiciones 1 y 2 no son alcanzables mediante procesamiento del padrón
existente: requieren el componente de campo ya identificado en el diagnóstico del
archivo DGN de Caranavi, o el registro alfanumérico con asignación de manzana que
se solicitó al GAM. Este ADR no fija plazo para ello.

## Cifras canónicas (resueltas el 2026-07-28)

La discrepancia de cobertura entre los commits `8b9fe10` (4.504 predios, 68,5 %)
y `a32a4b4` (68,2 %, equivalente a 4.486) quedó resuelta por consulta directa
sobre los datos efectivamente cargados en la versión activa v3 de Caranavi:

| Métrica | Valor |
|---|---|
| Filas totales | 6.573 |
| Con `cod_pred` | 4.486 |
| Sin `cod_pred` | 2.087 |
| Valores distintos de `cod_pred` | 96 |
| Rango de `cod_pred` | 0 - 952 |

**4.486 es la cifra canónica.** 4.504 era errónea. Se verifica por tres vías
coincidentes: conteo de no nulos, complemento de los nulos
(6.573 - 2.087 = 4.486) y el porcentaje declarado en 3.B.4.

## Consecuencias

- 3.B.7 se considera completable con el alcance delimitado arriba, mediante el
  paquete de cuatro capas descrito en ADR 0063.
- El cierre de fase 3.B debe declarar explícitamente que Caranavi no tiene
  maestro predial.
- La promoción a `TipoCapa.Predios` constituirá una fase de trabajo propia, con
  su ADR de migración de datos, y no una continuación de 3.B.

## Actualización 2026-07-28 — datos verificados en la importación real

La importación de `PRE_NOF_CAR` a la versión v3 activa de Caranavi aportó tres
datos que no estaban disponibles al redactar este ADR y que refuerzan sus
condiciones de promoción.

### El archivo de origen no contiene `cod_uv` ni `cod_man`

`ogrinfo` sobre `PRE_NOF_CAR.shp` declara únicamente dos campos de atributo:

    cod_pred: Integer (9.0)
    cod_geo:  String (80.0)
    Feature Count: 6573

El perfil `caranavi-versionado-predios-no-fotografiados` mapea cuatro campos,
pero dos de ellos no tienen columna de origen. Como el carril
`PrediosNoFotografiados` resuelve mediante `AEnteroOpcional`, la ausencia no
produce error: el mapeador asigna null y la carga se completa. Verificado en
base tras la importación, `cod_uv` y `cod_man` tienen **cero** valores no nulos
sobre las 6.573 filas.

`codigo_geografico` está poblado en las 6.573 filas, pero con el valor constante
`022001` —el código INE del municipio—, por lo que no identifica predios.

**Este es un modo de fallo silencioso del carril opcional**: una columna mapeada
que no existe en el DBF de origen produce nulos sin advertencia alguna en el
preview. Debe verificarse el esquema del `.dbf` contra el perfil antes de toda
importación por este carril.

### `cod_pred` es número de lote, no identificador

Distribución medida sobre la versión activa: 2.087 nulos y, entre los poblados,
478 filas con valor 1, 417 con valor 4, 410 con 2, 401 con 3, 350 con 5, en
curva decreciente casi monótona. Solo 96 valores distintos en un rango de 0 a
952.

El campo es el número de lote dentro de su manzana. Esto reinterpreta la
"colisión máxima de 478" registrada en 3.B.4: no era una anomalía de datos, sino
el conteo de predios cuyo lote es el 1, algo esperable con 637 manzanas.

**Consecuencia sobre las condiciones de promoción**: la condición 2 (cero
colisiones del triplete) no depende de limpiar `cod_pred`, sino enteramente de
obtener `cod_man`. Sin manzana, un número de lote no discrimina predios, y la
cobertura del 68,2 % es una métrica considerablemente más débil de lo que su
enunciado sugiere.

### Calidad geométrica confirmada

Cero geometrías nulas, cero inválidas, SRID 32719 uniforme en las 6.573 filas.
El shapefile generado en 3.B.6 no requiere saneo. Las 78 self-intersections
observadas en el preview (código O1) pertenecen a `capa_manzanas` y proceden del
mismo archivo fuente que ya alimentaba la v2; no son atribuibles a esta capa.

### Alcance entregado, confirmado en ejecución

La activación de la v3 dejó `dominio.predios` sin ningún registro de `022001` y
registró el motivo de forma explícita en `resumen_reconciliacion`:

    {"altas": 0, "omitida": true, "ausencias": 0, "sinCambio": 0,
     "actualizadas": 0, "motivoOmision": "Esquema municipal sin capa de predios."}

El visor renderiza las 6.573 geometrías y expone `capacidades.tienePredios:
false`. La delimitación de alcance descrita en este ADR queda confirmada por el
comportamiento observado del sistema, no solo por lectura de código.
