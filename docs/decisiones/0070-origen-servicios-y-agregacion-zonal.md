# ADR-0070 — Origen del dato de servicios y agregación zonal de los índices

- **Estado:** Aceptada **como decisión de diseño**. Ver la limitación de alcance
  inmediatamente abajo.
- **Fecha:** 2026-08-03 · Fase 4.B.
- **Corrige** dos erratas de D7 de ADR-0066 (§D1).
- **Refina** D3 de ADR-0066 (cadena zonal) y complementa D5 de ADR-0069
  (asignación predio→vía).
- **Precede a M016.** Sus ocho parámetros entran al catálogo de D6 de ADR-0069.
- **Evidencia:** cinco tareas de solo lectura sobre la versión activa de Uyuni
  (`b6934919-62fa-40ed-b557-d94a01cd9d65`, `051201`, 11.985 predios), más la
  transcripción literal del párrafo y la tabla de `Fs` de la p. 37 impresa de
  RM 024/2024 aportadas por el orquestador.
- **Auditoría:** una tarea de reproducción independiente, ejecutada sin recibir
  ningún valor esperado, confirmó las cifras `[V]`. Una auditoría documental
  posterior corrigió dos cifras de este borrador, su numeración y nueve
  afirmaciones que mezclaban medición con criterio. Todas están incorporadas.

> **Limitación de alcance de la aceptación.** Este ADR fija el **diseño**: qué
> fuente se usa, cómo se agrega y qué se declara. **No promueve ninguno de sus
> parámetros a valor municipal vigente.** El sistema puede implementarlos y
> reproducirlos, pero no puede presentar el resultado como valuación vigente ni
> habilitar emisión tributaria hasta que concurran dos condiciones: aprobación
> del GAM sobre los parámetros marcados `[C]`, y resolución de la dependencia
> externa E3 (§Consecuencias).

---

## Contexto

ADR-0066 D3 adoptó los coeficientes tabulados de RM 024/2024 y ADR-0066 D7
decidió replicar el doble cómputo de servicios que la norma establece: `IPIU` a
nivel de zona y `Fs` a nivel de predio. Ninguna de las dos decisiones examinó de
dónde sale el dato de servicios ni cómo se agrega a nivel zonal.

Este ADR resuelve las dos preguntas.

Punto de partida `[V]`:

- Las cuatro columnas de servicio están en `dominio.capa_parcelas`, no en
  `dominio.predios`. Son `servicio_agua`, `servicio_luz`,
  `servicio_alcantarillado` y `servicio_telefonia`.
- Su tipo es `character varying`, no `boolean`.
- Su vocabulario tiene exactamente tres estados: `SI`, `NO` y cadena vacía. No
  hay ningún `NULL` en ninguna de las cuatro columnas.
- `dominio.capa_vias` tiene nueve columnas y **ninguna de servicios**; su
  `atributos_extra` está vacío en todas las filas.
- `capa_parcelas` contiene dos versiones de dataset de 11.985 filas cada una.
  Toda consulta debe filtrar por `dataset_version_id`.

---

## Decisiones

### D1 — Corrección de D7 de ADR-0066: el estado de ausencia es la cadena vacía

D7 de ADR-0066 afirma que las columnas de servicio son *"4 columnas booleanas"*
y establece la regla *"`NULL` no es `NO`. Un `NULL` bloquea el cálculo y marca el
predio como observado"*, citando **245 parcelas con los cuatro servicios en
`NULL`**.

`[V]` Medido: las columnas son `character varying`, y el conteo de filas con los
cuatro servicios en `NULL` es **cero**, tanto sobre la versión activa como sobre
la tabla completa. Las 245 parcelas existen y su patrón es correcto, pero sus
cuatro valores son **cadena vacía**, no nulos.

**Consecuencia:** la regla de D7, implementada al pie de la letra, no se dispara
nunca. Los 245 predios que debía enviar a observación pasarían de largo.

**Decisión:** la regla se reenuncia sobre **valor no informado**, definido como
`NULL` o cadena vacía tras `trim`. El predicado canónico de servicio presente es
`servicio_x = 'SI'`; el de servicio ausente es `servicio_x = 'NO'`; cualquier
otro valor es no informado y bloquea el cálculo automático.

`[V]` Alcance del carril: **324 predios (2,7%)** tienen al menos un servicio no
informado. De ellos, 245 tienen los cuatro.

Se declara además que **ningún otro valor aparece hoy en el vocabulario**. Si una
versión futura introdujera uno, el motor debe fallar y no imputar.

### D2 — `Fs` se calcula por conexión del predio, sustituyendo una fuente inexistente

`[V]` Transcripción literal de la p. 37 impresa (p. 43 del PDF):

> *"En el caso de los servicios, de acuerdo a los disponibles en la vía, se asume
> uno o más coeficientes que se suman, cuando se tiene todos los servicios, se
> obtiene 1, cuando no se tiene ninguno, se asume el mínimo que es 0,20"*

`[V]` La tabla contigua al párrafo tabula los coeficientes: mínimo 0,20, y 0,20
para cada uno de agua potable, energía eléctrica, alcantarillado y otros
servicios. **El reparto uniforme está tabulado, no inferido:**
`Fs = 0,20 + 0,20 · n`, máximo 1,00.

La norma pide **disponibilidad en la vía**. Las columnas de `capa_parcelas`
registran, con toda probabilidad, **conexión del predio**. No es lo mismo: un
predio sobre una calle con red de agua que nunca hizo su conexión tiene el
servicio disponible y aparece como `NO`.

`capa_vias` no tiene atributos de servicio, así que **la fuente prescrita no
existe en el catastro de Uyuni**. La sustitución es forzada, no elegida.

`[C]` **Naturaleza del problema.** La norma **no es ambigua**: exige
disponibilidad en la vía. Lo indeterminado no es el texto normativo sino **cómo
sustituir un insumo ausente**. Este ADR no elige entre dos lecturas admisibles;
elige un sustituto para un dato que la norma requiere y el catastro no tiene. Es
una posición más débil que la de una interpretación normativa, y la ordenanza
debe declararla como tal.

Se ensayaron tres operacionalizaciones. Dos se descartan (ver D4).

`[V]` Distribución medida de `Fs` bajo las dos lecturas computables. Los conteos
son medición directa; su identificación con `Fs` depende de los criterios de este
ADR y de D3:

| `Fs` | Por conexión del predio | Por disponibilidad en la manzana |
|---|---|---|
| 0,20 | 4.078 | 718 |
| 0,40 | 359 | 175 |
| 0,60 | 6.322 | 6.163 |
| 0,80 | 1.211 | 4.677 |
| 1,00 | 15 | 252 |
| **media** | **0,4786** | **0,6596** |

`[V]` Solo **5.797 predios (48,4%)** reciben la misma banda bajo ambas lecturas.

**Decisión:** `Fs` se calcula por **conexión del predio**, con
`fuente_servicios = conexion_predial` y `requiere_validacion_oficial = true`. La
sustitución de fuente se declara expresamente en la ordenanza junto con las dos
cifras.

#### Por qué la preferencia asimétrica de ADR-0068 no aplica aquí

ADR-0068 eligió deliberadamente el error que perjudica al contribuyente, porque
*"los sobrecompensados no reclaman nunca y el municipio resigna recaudación sin
enterarse"*. La lectura por conexión hace lo opuesto: **favorece al contribuyente
y hace que el municipio resigne recaudación.**

La aparente contradicción se disuelve al mirar qué distinguen los dos casos. En
ADR-0068, los dos juegos de cortes clasificaban **la misma geometría del propio
predio**; el desacuerdo era sobre dónde cortar, no sobre qué hecho atribuirle.
Aquí, la opción de mayor avalúo exige **atribuir al predio un servicio observado
únicamente en terceros**.

**Regla de excepción, que se declara con alcance general:**

> La preferencia recaudatoria de ADR-0068 **no es una regla universal**. No se
> aplica cuando la opción de mayor avalúo exige imputar al predio un hecho
> observado únicamente en terceros.

`[C]` **Relación con la regla de herencia de D5 de ADR-0069.** Aquella regla hace
que 1.443 predios sin vía a 15 m hereden el material observado en las vías de
otros predios de su manzana: también es imputación desde terceros. **No queda
alcanzada por la excepción**, porque la herencia no produce sistemáticamente el
mayor avalúo —hereda el material que haya, y el 90% de las vías de Uyuni es
tierra con `IPV = 1,00`. La excepción se activa por la conjunción de imputación
**y** dirección alcista, no por la imputación sola. Se deja escrito para que la
adyacencia entre ambas reglas no se lea como inconsistencia.

`[C]` **Las dos cifras no acotan el `Fs` normativo verdadero.** Son resultados de
dos sustitutos computables. La conexión es plausiblemente inferior a la
disponibilidad real; la manzana no es demostrablemente superior, porque un tramo
de vía sirve a predios de dos manzanas. **No hay cota demostrada.** Lo medido es
que las dos sustituciones difieren un 37,8% en su media.

`fuente_servicios` se versiona por municipio. Cuando exista campaña de encuestas
con pregunta propia de disponibilidad en vía, se sustituye sin tocar código.

### D3 — Telefonía materializa "Otros servicios" para Uyuni

RM 024/2024 es contradictoria sobre la cuarta categoría: la p. 37 la denomina
*"Otros servicios"* sin definirla; la p. 50 enumera el teléfono entre los
servicios urbanos, pero la tabla de esa misma página coloca "Gas Domiciliario"
en el cuarto lugar.

**Decisión:** para Uyuni, y mientras no exista catálogo municipal más específico,
`servicio_telefonia = 'SI'` materializa la categoría *"Otros servicios"* de `Fs`.
La correspondencia es **criterio municipal de operacionalización versionado**
`[C]`, no equivalencia normativa `[V]`, y es sustituible por ordenanza.

Fundamento: excluir telefonía sin otra fuente para la cuarta categoría obliga a
elegir entre dos males. O se conserva 0,20 por servicio y el techo efectivo de
`Fs` baja a 0,80, con lo que Uyuni queda deflactado un 20% frente a cualquier
municipio con cuatro categorías computables. O se redistribuye a 0,2667 por
servicio para preservar la escala, y entonces se contradice la tabla de la p. 37,
que fija 0,20 por categoría.

`[V]` Efecto medido: 30 predios con `servicio_telefonia = 'SI'` en 11.985. Solo
**15 predios** alcanzan `Fs = 1,00` en todo el municipio. El efecto recaudatorio
es despreciable; lo que se compra es coherencia de escala.

### D4 — Dos operacionalizaciones descartadas, con su fundamento

**Disponibilidad en la manzana** — descartada como base. Una manzana está
bordeada por cuatro calles; que la red llegue a una no implica que llegue a las
cuatro. Se conserva como referencia de contraste, no como cota.

**Disponibilidad en el tramo de vía a 15 m** — descartada por medición. Se
declaró antes de ejecutarla que sería la lectura preferida si su media caía entre
las otras dos. `[V]` Resultado: **0,6688 sobre 10.273 predios**, por encima de la
lectura por manzana. Un tramo de vía toca predios de las dos manzanas que
enfrenta, de modo que agrega servicios a través de manzanas.

`[V]` Caso extremo asociado: **92 predios** pasan de `n = 0` por conexión a
`n = 4` por tramo, o sea de `Fs = 0,20` a `Fs = 1,00`. `[V]` Están concentrados:
83 de los 92 en el distrito 1, y **34 de ellos en dos manzanas** (17 en `1-11` y
17 en `1-29`).

`[C]` Esa concentración no está explicada. Podría tratarse de manzanas reales de
lotes sin conectar frente a calles servidas —en cuyo caso la lectura por tramo
estaría capturando algo cierto— o de artefactos de la unión espacial. **No se
midió.** El descarte de la lectura por tramo **no descansa en estos 92 casos**,
sino en el argumento estructural del párrafo anterior.

**Agrupación por `capa_parcelas.nombre_via`** — descartada. `[V]` El campo tiene
2.100 nombres distintos para 11.728 predios, 1.561 de ellos con un solo predio, y
**148 nombres aparecen en más de un distrito**. `[C]` Los treinta valores más
frecuentes (`CABRERA` 324, `FERROVIARIA` 279, `POTOSI` 278) *tienen forma* de
nombres de calle normalizados, pero eso es apreciación textual: no existe
diccionario ni reconciliación que lo demuestre. Lo que descalifica al campo para
esta agregación es que una calle es una entidad lineal larga y agrupar por su
nombre uniría el centro con la periferia. Queda anotado como posible llave de
reconciliación con `capa_vias.nombre` (ver Pendiente 4).

`[C]` Dos ejecuciones distintas devolvieron 2.101 y 2.100 nombres, y 2.379 y
2.373 pares nombre-distrito. La diferencia no fue explicada. **Se cita la
medición de la auditoría.**

### D5 — `IPIU`: umbral de presencia zonal del 50% sobre observaciones válidas

RM 024/2024 prescribe sumar 0,20 por servicio existente en la zona, pero **no
define qué proporción de observaciones prediales convierte un servicio en
presencia zonal**. Sin esa regla, `IPIU` no es computable.

**Decisión:**

```
presencia(servicio, zona) = n(SI) / [ n(SI) + n(NO) ]
presente  si  presencia >= umbral_presencia_zonal_ipiu
```

con `umbral_presencia_zonal_ipiu = 0,50` para Uyuni. Los valores no informados
**no cuentan como `NO`**: quedan fuera del denominador. Si una zona carece de
observaciones válidas suficientes, el índice queda pendiente y no se imputa.

`[V]` Medición sobre la versión activa, con denominador de observaciones válidas:

| Zona | Predios | Agua | Luz | Alcantarillado | Telefonía |
|---|---|---|---|---|---|
| A | 2.166 | 73,55% | 73,50% | 35,02% | 0,86% |
| B | 3.865 | 89,93% | 89,89% | 9,27% | 0,03% |
| C | 2.725 | 61,87% | 60,28% | 3,78% | 0,41% |
| D | 3.229 | 37,19% | 33,75% | 2,17% | 0,00% |

**`IPIU` resultante: A 1,40 · B 1,40 · C 1,40 · D 1,00.** No es una medición
autónoma: es el resultado verificable de aplicar el umbral `[C]` a la
distribución `[V]`.

Fundamento del corte:

1. Traduce *presencia zonal* como disponibilidad predominante, no como
   existencia de un caso aislado.
2. Evita atribuir agua y electricidad a toda la zona D cuando el 62,8% y el
   66,3% de sus observaciones válidas no las reportan.
3. `[V]` **Existe un intervalo de indiferencia medido.** El valor más alto entre
   los pares clasificados como ausentes es 37,19% (agua en D) y el más bajo entre
   los presentes es 60,28% (luz en C). **Cualquier umbral en `(37,19% ; 60,28%]`
   produce exactamente la misma tabla.** El 0,50 es el punto convencional y
   auditable dentro de esa banda.

`[C]` **La banda protege agua y luz, no alcantarillado.** El alcantarillado de la
zona A está en 35,02%, a menos de 16 puntos del umbral y sobre una pendiente
continua hacia las otras zonas. Un corte del 30% le daría alcantarillado a la
zona A y movería su `IPIU` de 1,40 a 1,60.

`minimo_observaciones_zonal` se declara como parámetro acompañante. `[V]` En
Uyuni el denominador más pequeño es de 2.092 observaciones, de modo que el
parámetro es inerte aquí. Se declara porque la llave del catálogo zonal es
`(municipio_codigo, nombre_zona)` (D6 de ADR-0066) y un municipio futuro puede
tener una zona con tres predios relevados, donde dos observaciones y un `SI`
darían 50% y acreditarían el servicio.

### D6 — Los tres índices zonales exigen **tres reglas de agregación distintas**

`VSz = VPz · ((IPIU + IPES + IPRT) / 3)` promedia tres índices que miden cosas de
naturaleza distinta. Aplicarles la misma regla de agregación zonal sería un error.

| Índice | Qué mide | Naturaleza | Regla de agregación |
|---|---|---|---|
| `IPIU` | infraestructura de red | proporcional | umbral de presencia (D5) |
| `IPRT` | topografía del terreno | categórica única | categoría dominante |
| `IPES` | equipamiento urbano | existencial | presencia de al menos uno |

**`IPRT`** no admite umbral de presencia porque no es un conjunto de atributos
independientes: cada predio tiene exactamente una topografía. La regla es la
categoría modal de las observaciones válidas de la zona.

`[V]` Medición: `PLA` es dominante en las cuatro zonas con más del 99% de las
observaciones válidas en cada una (A 2.124, B 3.765, C 2.670, D 3.172; total
**11.731**, cifra que reproduce la declarada en D2 de ADR-0066). `[V]` 224
predios tienen topografía no informada y quedan fuera del denominador.
**`IPRT = 1,20` para las cuatro zonas**, resultado de aplicar la regla modal
`[C]` a esa distribución.

**`IPES`** no admite umbral de presencia porque el equipamiento urbano es
existencial: un solo hospital hace que la zona tenga equipamiento de salud.
Aplicarle el criterio del 50% dejaría a Uyuni sin ninguna categoría acreditada e
`IPES = 1,00` en todo el municipio, lo que sería falso. `[V]` Los predios con uso
`SAL` son 9 en 11.985; con `EDU`, 16.

`[D]` La auditoría reporta que la norma habla de *"existencia de uno o más
equipamientos"*, lo que daría apoyo textual a esta regla. **La cita literal y su
página no fueron verificadas.** Hasta que lo sean, la regla se mantiene marcada
como criterio.

**Decisión:** las tres reglas se declaran por separado en el catálogo de
parámetros.

### D7 — `via_mat` es fuente complementaria del carril de excepción de `Mv`

`[V]` `capa_parcelas.atributos_extra->>'via_mat'` está poblado en **11.754 de
11.985 predios (98,1%)**, con vocabulario de ocho materiales: `TRR` 10.173, `LOS`
1.028, `ADQ` 446, `LAD` 64, `ASF` 29, `PIE` 6, `RIP` 4, `CEM` 4.
`capa_vias.material` tiene solo cuatro.

D5 de ADR-0069 sostuvo que no existe clave entre predios y vías y construyó toda
la asignación espacial sobre esa premisa. La premisa es correcta para
`dominio.predios`, pero el **propósito** de esa clave —obtener `IPV`— ya estaba
servido por un atributo del levantamiento original que no se había examinado.

`[V]` Concordancia medida en términos de `IPV`, no de material, porque adoquín,
loseta y cemento comparten `IPV = 1,15`:

```
predios con valor en ambas fuentes      10.019
concordantes                             9.308   92,90%
discordantes                               711    7,10%
  de ellos, capa_vias mayor                390
  de ellos, via_mat mayor                  321
```

`[C]` El reparto casi simétrico de las discordancias sugiere ausencia de sesgo
sistemático entre fuentes. La interpretación no está verificada.

`[V]` **Validación cruzada de ADR-0069 D5:** los predios sin vía a 15 m suman
**1.712**, que es exactamente la suma de las tres clases de excepción declaradas
en aquel ADR (1.443 heredados + 157 en QC + 112 sin material). Reproducción
independiente por consultas escritas con otro criterio de agregación, en tres
tareas distintas.

`[V]` De esos 1.712 predios, **1.684 tienen valor en `via_mat`**.

**Decisión:** la asignación espacial de D5 de ADR-0069 se mantiene como fuente
primaria. `via_mat` se usa como **fuente complementaria para el carril de
excepción**, y el avalúo declara cuál de las dos originó el `Mv` aplicado,
conforme a D3 de ADR-0069. Donde ambas existen y discrepan, el predio va a QC.

`[V]` **`LAD` (ladrillo) no figura en la tabla `IPV` de RM 024/2024.** Son 64
predios. Requiere asimilación municipal por ordenanza.

### D8 — Disparador de QC: telefonía como único servicio

`[V]` Existe **un predio** en Uyuni, `1-29-2` de 153,22 m², con
`servicio_telefonia = 'SI'` y los otros tres en `NO`.

Bajo D3 su `Fs` pasa de 0,20 a 0,40: **el doble de avalúo de terreno que un
predio de igual superficie y zona sin ningún servicio.** `[C]` No se midió si sus
vecinos inmediatos son comparables en superficie, forma o uso.

**Decisión:** `Fs` no se asigna automáticamente cuando la cuarta categoría es el
único servicio presente. Motivo tipificado `FS_UNICO_SERVICIO_OTROS`, que se
agrega a la lista de D7 de ADR-0069.

---

## Los ocho parámetros que este ADR aporta al catálogo

Se suman a los siete de D6 de ADR-0069. Todos son datos versionados por
municipio, ninguno es constante compilada, y **ninguno es universal**.

| Parámetro | Uyuni | Marca | Origen |
|---|---|---|---|
| `fuente_servicios` | `conexion_predial` | `[C]` | D2 |
| `requiere_validacion_oficial` | `true` | — | D2 |
| `mapeo_otros_servicios` | `servicio_telefonia` | `[C]` | D3 |
| `umbral_presencia_zonal_ipiu` | 0,50 | `[C]` | D5 |
| `minimo_observaciones_zonal` | por fijar | `[C]` | D5 |
| `regla_agregacion_iprt` | `categoria_dominante` | `[C]` | D6 |
| `regla_agregacion_ipes` | `existencial` | `[C]` | D6 |
| `ipv_material_lad` | sin asimilar | — | D7 |

**Seis están marcados `[C]`.** La evidencia los acota; ninguno se deduce de la
norma. `requiere_validacion_oficial = true` es el mecanismo que impide que una
corrida basada en ellos se presente como valuación vigente antes de la aprobación
del GAM.

---

## Consecuencias

- `Fs` queda **calculable para el 97,3%** de los predios de Uyuni, con 324 en el
  carril de QC por valor no informado y 1 por telefonía única.
- **`IPES` está bloqueado por falta de diccionario semántico.** `[V]` Existen dos
  fuentes candidatas y ninguna es operativa: `capa_parcelas.uso_terreno` tiene
  códigos sin diccionario, y `dominio.catalogo_uso_suelo` está normalizado pero
  **sin enlace**: `dominio.predios.uso_suelo_id` está poblado en **0 de 11.985**
  filas y hay **0 predios enlazados al catálogo**. `[V]` ADR-0056 enumera los
  quince códigos de origen pero no su significado, y `CatalogoPresentacionMunicipal.cs`
  conserva deliberadamente sus etiquetas en `null` con la leyenda *"diccionario
  oficial pendiente"*.

### Se declara una tercera dependencia externa del GAM

ADR-0069 declaró que `Mv` no agregaba una tercera dependencia, y **esa
afirmación era correcta para `Mv`**. Este ADR descubre una distinta, procedente
de `IPES`. ADR-0069 no queda invalidado.

**Criterio de conteo de dependencias**, que se adopta con alcance general: una
dependencia es externa cuando cumple los tres requisitos —solo una autoridad
externa puede resolverla, el equipo técnico no puede inferirla legítimamente, y
su ausencia bloquea el cálculo. **Se cuenta por esos criterios, no por el
esfuerzo que demande.**

| | Dependencia | Naturaleza | Destraba |
|---|---|---|---|
| **E1** | Campaña de encuestas | operativa | `VPz` / `Vz` |
| **E2** | Valores constructivos aprobados por ordenanza | normativa | `Tip` |
| **E3** | Diccionario `uso_terreno` → categorías `IPES` | **documental liviana** | `IPES` y `VSz` |

E3 se denomina *documental liviana* para distinguirla de una campaña de campo,
pero **no se oculta**: bloquea `IPES` y con él `VSz`.

- `[V]` **Cero predios quedan fuera de zona.** Los 11.985 caen dentro de algún
  polígono de `capa_zonas`, lo que reproduce sobre la versión activa lo que
  ADR-0045 había anotado sobre archivos.
- **La campaña de encuestas de ADR-0067 debe incorporar una pregunta de
  disponibilidad de servicio en la vía, separada de la conexión del predio.** Es
  una casilla en el formulario que resuelve la sustitución de D2. Ni ADR-0067 E1
  ni D9 de ADR-0045 la contemplan.

---

## Limitaciones declaradas

### L1 — La fuente que la norma prescribe no existe

Tres sustituciones ensayadas, ninguna mide *"disponible en la vía"*. **El insumo
que la norma exige no está en el catastro de Uyuni.** No se trata de elegir entre
lecturas admisibles del texto sino de suplir un dato ausente, y así debe
declararse ante el GAM.

### L2 — La diferencia entre sustituciones es grande y no está acotada

`[V]` Las medias de las dos sustituciones computables difieren un **37,8%**. `[C]`
Ninguna es cota demostrada del valor normativo verdadero. Mitigación: la ordenanza
se aprueba con la tabla de D2 adjunta y con la declaración de L1.

### L3 — El intervalo de indiferencia del umbral zonal no cubre el alcantarillado

Documentado en D5.

### L4 — La concentración de los 92 predios no está explicada

Documentado en D4. 83 de 92 en el distrito 1, 34 en dos manzanas.

### L5 — 1.180 predios edificados sin ningún servicio

`[V]` La covariación entre servicios y edificación es monótona sin excepción
(28,9% · 42,3% · 79,7% · 89,4% · 93,3% de predios con edificación según su conteo
de servicios). Dentro de ese patrón, 1.180 predios tienen edificación y cero
servicios. `[C]` Es plausible en el altiplano y no constituye por sí mismo un
defecto. Merece inspección visual por muestra, no consulta.

### L6 — Todo esto es Uyuni

Los ocho parámetros salen de un municipio. Caranavi no tiene ninguno calibrado y
sus 6.573 predios siguen bloqueados por los 11 con astillas (L5 de ADR-0068).
**Ningún valor de Uyuni se usa como predeterminado para otro municipio.**

---

## Criterios de diseño, no consecuencias de la medición

La evidencia acota; no determina. Se listan las decisiones que un lector futuro
podría cambiar sin contradecir ningún dato.

**Criterios de operacionalización** — elegir la conexión predial (D2) · mapear
telefonía a "Otros servicios" (D3) · fijar el umbral zonal en 0,50 dentro de su
banda de indiferencia (D5) · las tres reglas de agregación (D6) · usar `via_mat`
como complementaria y no como primaria (D7) · el disparador de QC por telefonía
única (D8) · el criterio de conteo de dependencias externas (§Consecuencias).

Dos merecen fundamento explícito:

**La elección de la conexión predial.** Lo que la medición aporta es la
diferencia entre sustituciones; la elección es criterio. Su fundamento es la
regla de excepción de D2 —la imputación desde terceros no puede ser el vehículo
del mayor avalúo—, que es un principio, no un balance de conveniencias. Un
municipio podría adoptar la lectura por manzana y recaudar sensiblemente más sin
contradecir ninguna medición, y por eso el parámetro nace con
`requiere_validacion_oficial = true`.

**Las tres reglas de agregación de D6.** Ninguna medición obliga a usar reglas
distintas; la uniformidad sería más simple de explicar. El argumento es que
`IPES` con umbral proporcional daría 1,00 en todo Uyuni, lo cual es
demostrablemente falso, y que `IPRT` con umbral proporcional no está definido
sobre una variable categórica única.

---

## Pendiente

1. **Acotar el alcance real de E3.** `[C]` Hipótesis no medida: tres de las cinco
   categorías de `IPES` podrían ser determinables sin diccionario —salud (`SAL`),
   cultural-educativo-recreativo (`EDU`, `CUL`, `REC`, `DEP`) y comercial
   (`COM`)—, quedando solo gestión (`OFI`) y transporte (`TRU`) dependientes de
   él. Si eso se confirmara **por zona**, la incertidumbre de `IPES` sería de
   0,10 y la de `VSz` de 3,3%, y E3 pasaría de bloqueante a precisión pendiente.
   **Los conteos disponibles son municipales, no zonales; la hipótesis no puede
   afirmarse sin medirla por zona.** `SER` (45) y `CMC` (3) quedan sin asignar en
   cualquier escenario.
2. **Obtener el diccionario semántico municipal de `uso_terreno`** (E3). El
   vocabulario observado es `VIV`, `TRR`, `SIN`, `COM`, `SER`, `TRU`, `EDU`,
   `OFI`, `DEP`, `SAL`, `REC`, `IND`, `REL`, `CMC`, `CUL`.
3. **Fijar `minimo_observaciones_zonal`** e `ipv_material_lad` por ordenanza.
4. `[C]` **Evaluar la reconciliación `capa_parcelas.nombre_via` ↔
   `capa_vias.nombre`** como camino para individualizar tramos por manzana, que
   es lo que la Guía prescribe en pp. 22 y 28 y que D5 de ADR-0069 declaró
   imposible por falta de clave.
5. **Verificar la cita literal y la página** del texto que sustentaría la regla
   existencial de `IPES` (D6), hoy marcada `[D]`.
6. **Inspección visual por muestra de los 1.180 predios edificados sin
   servicios** (L5).
7. **Explicar la concentración de los 92 predios** del distrito 1 (L4).
8. **Explicar la divergencia entre las dos mediciones de `nombre_via`**
   (2.101/2.379 contra 2.100/2.373).
