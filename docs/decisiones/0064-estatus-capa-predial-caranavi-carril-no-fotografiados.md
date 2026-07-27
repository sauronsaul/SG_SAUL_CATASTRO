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

## Cifras pendientes de conciliación

Existe una discrepancia sin resolver en la cobertura de `cod_pred` entre dos
commits de la misma fase:

- `8b9fe10` (3.B.3): 4.504 predios con `cod_pred`, 68,5 %.
- `a32a4b4` (3.B.4): 68,2 %, equivalente a 4.486 predios.

Ambas cifras son internamente coherentes con su porcentaje sobre 6.573. La
diferencia de 18 filas debe explicarse y fijarse una cifra canónica antes de que
cualquiera de las dos se use como línea base para medir la condición 1. Hasta
entonces, este ADR no adopta ninguna de las dos.

## Consecuencias

- 3.B.7 se considera completable con el alcance delimitado arriba, mediante el
  paquete de cuatro capas descrito en ADR 0063.
- El cierre de fase 3.B debe declarar explícitamente que Caranavi no tiene
  maestro predial.
- La promoción a `TipoCapa.Predios` constituirá una fase de trabajo propia, con
  su ADR de migración de datos, y no una continuación de 3.B.
