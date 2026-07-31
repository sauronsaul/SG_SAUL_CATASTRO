# ADR-0068 — Calibración del factor forma `Ff` para Uyuni

- **Estado:** Aceptada.
- **Fecha:** 2026-07-30 · Fase 4.A.
- **Refina:** D5 de ADR-0066, que fijó la métrica pero dejó los umbrales marcados
  `[C]` ("pendiente de medición"). Este ADR los mide y cierra esa marca.
- **No modifica** ninguna otra decisión de ADR-0066 ni de ADR-0067.
- **Alcance:** los seis parámetros calibrados aquí valen para Uyuni (`051201`).
  Cada municipio requiere su propia calibración (ver L5).
- **Evidencia:** tareas 4.A.12 a 4.A.16, solo lectura, sobre la versión activa de
  Uyuni (`051201`, 11.985 predios, todos `POLYGON`, 0 inválidos, 0 huecos).
  PostGIS 3.4.3. Inspección visual de 5 predios en el visor institucional.
- **Auditoría:** las cifras de §Decisión y §Limitaciones fueron reproducidas por
  consulta independiente en la tarea 4.A.16, que corrigió seis cifras del borrador
  inicial. **Las cifras de §Pendiente están marcadas
  individualmente según su estado de verificación**; varias no están medidas.

---

## Contexto

RM 024/2024 p. 37 tabula `Ff` en tres valores —regular 1,00, irregular 0,90, muy
irregular 0,80— **sin definir qué mide "irregular" ni dónde cortar**. Para
valuación masiva sobre 11.985 predios eso es inaplicable: la operacionalización es
una decisión municipal, y este ADR la documenta.

D5 de ADR-0066 ya descartó Polsby-Popper por medir la propiedad equivocada:
confunde elongación con irregularidad, y ordena un lote en L (PP 0,589) por encima
de un rectángulo perfecto de fondo 1:4 (PP 0,503). Lo que quedaba abierto era la
métrica de reemplazo y sus umbrales.

---

## Decisión

### 1. Criterio primario: conteo de vértices efectivos

```
nv = ST_NPoints(ST_ExteriorRing(ST_SimplifyPreserveTopology(geometria, 0.05))) - 1
```

| Banda | `Ff` | Predios | % | Demérito mediano |
|---|---|---|---|---|
| `nv` ≤ 7 | 1,00 | 11.038 | 92,1% | 0,75% |
| `nv` 8–9 | 0,90 | 545 | 4,5% | 8,11% |
| `nv` ≥ 10 | 0,80 | 402 | 3,4% | 24,85% |

*Demérito mediano* = `1 − mediana(rec)` del grupo: la superficie útil que el lote
típico pierde respecto de su rectángulo mínimo envolvente. Se usa **mediana** y no
media porque la distribución tiene cola izquierda larga y la media sobreestima el
caso típico (medias: 1,99% / 13,49% / 25,83%).

Se elige el conteo de vértices, y no una métrica geométrica continua, por **ser
auditable por el contribuyente y por el concejo municipal**: se verifica contando
linderos en el plano, sin SIG. RM 024/2024 está dirigida a GAM sin capacidades
técnicas (p. 3-4); un factor que solo un SIG puede explicar es un factor que el
municipio no puede defender.

**Nota sobre el cierre de anillo:** `ST_NPoints` incluye el punto de cierre
repetido. Un rectángulo devuelve 5. El `−1` es obligatorio; sin él toda la trama
ortogonal caería en "irregular".

### 2. La tolerancia de normalización es parámetro normativo, no constante de código

**0,05 m**, por debajo de la precisión del levantamiento.

No es un detalle de implementación. El efecto sobre la banda asignada es
**monótono en una sola dirección**: ningún predio se vuelve más irregular al
aumentar la tolerancia.

| Tolerancia | `nv` ≤ 7 → `Ff` = 1,00 | Predios con descuento |
|---|---|---|
| sin normalizar | 7.166 (59,8%) | 4.819 |
| **0,05 m** | **11.038 (92,1%)** | **947** |
| 0,25 m | 11.522 (96,1%) | 463 |

De 0,05 a 0,25, los predios que reciben descuento **pasan de 947 a 463: la mitad.**
Sin normalizar serían 4.819. **Sin declarar, la tolerancia es un mecanismo de
ajuste tributario invisible.** Por eso se fija por ordenanza junto con los umbrales.

### 3. Guarda por rectangularidad

```
rec = ST_Area(geometria) / ST_Area(ST_OrientedEnvelope(geometria))

Si rec >= 0,95  ->  Ff = 1,00, sin importar nv.
```

Fundamento: **293 predios** tienen `nv` ≥ 8 y `rec` ≥ 0,95 — lotes esencialmente
rectangulares cuyo contorno está muy segmentado, típicamente por seguir una curva.
Sin la guarda recibirían un demérito de 10% o 20% por un atributo geométricamente
neutro.

Explicable en una frase: *"su lote ocupa el 96% de su rectángulo envolvente, así
que es regular por más vértices que tenga su contorno."*

### 4. Carril de QC: tres disparadores, con precedencia

Un predio **no se valúa automáticamente** y pasa a revisión manual si cumple
cualquiera de estas condiciones. **El carril de QC tiene precedencia sobre la
asignación de banda**: un predio en QC no recibe `Ff` automático, esté en la banda
que esté.

```
sol = ST_Area(geometria) / ST_Area(ST_ConvexHull(geometria))

D1)  sol < 0,95                        ->  799 predios
D2)  rec < 0,80                        ->  +81 netos
D3)  nv >= 10  y  rec >= 0,95          ->   65 predios
                                       ---------------
                                Total:     945  (7,9%)
```

- **D1** detecta anomalías geométricas: entrantes profundos, astillas,
  autointersecciones. No detecta lotes irregulares convexos.
- **D2** es estructuralmente necesario, no una contingencia. La solidez es siempre
  mayor o igual que la rectangularidad, porque el casco convexo está contenido en
  el rectángulo envolvente. Por lo tanto **un lote convexo pero oblicuo es
  invisible para D1 por construcción**: hay 72 predios con `sol` ≥ 0,99 y `rec` <
  0,80, y el peor `rec` entre los casi convexos es **0,5000**, exactamente el valor
  de un triángulo. D2 **reduce el residuo silencioso de 158 a 83**.
- **D3** detecta contradicción entre métricas: contorno muy segmentado pero forma
  rectangular. Se envía a revisión en lugar de decidir por regla.

Alimenta el carril que **D8 de ADR-0045** ya estableció: los casos divergentes se
exhiben como *feature* de QC, no se ocultan.

### 5. Resultado de aplicar la regla completa

Dos filas distintas, que no deben confundirse:

| | regular | irregular | muy irregular | a revisión | total |
|---|---|---|---|---|---|
| **antes del QC** | 11.331 | 317 | 337 | — | 11.985 |
| **efectivamente valuados** | **10.920** | **81** | **39** | **945** | 11.985 |

`Ff` medio de los valuados: **0,9986**.

### 6. Los seis parámetros son datos versionados por municipio

| Parámetro | Valor para Uyuni |
|---|---|
| `tolerancia_normalizacion` | 0,05 m |
| `corte_regular` | 7 |
| `corte_irregular` | 9 |
| `umbral_guarda_rec` | 0,95 |
| `umbral_qc_solidez` | 0,95 |
| `umbral_qc_rect` | 0,80 |

Son **datos**, versionados por municipio y por versión de fórmula, conforme a D6 de
ADR-0066. No son constantes compiladas. D3 no introduce parámetro propio: reutiliza
`corte_irregular` y `umbral_guarda_rec`. `umbral_guarda_rec` y `umbral_qc_solidez`
valen 0,95 los dos pero son conceptualmente distintos —uno sobre `rec`, otro sobre
`sol`— y se declaran separados.

**Se almacena la medición, no el factor derivado.** `nv`, `rec` y `sol` se
persisten; `Ff` se deriva en tiempo de valuación. Si una ordenanza mueve un umbral,
no hay que recalcular 11.985 filas ni decidir qué pasa con las liquidaciones ya
emitidas. Es la misma lógica de `superficie_sig` (D4 de ADR-0066).

---

## Por qué estos cortes y no `nv` ≤ 4

Se evaluaron dos juegos. La medición decide por el demérito real.

| Juego | Banda intermedia | Predios | Demérito mediano | `Ff` asignado |
|---|---|---|---|---|
| **A** | `nv` 5–7 | 3.114 | **1,73%** | 0,90 |
| **B** | `nv` 8–9 | 545 | **8,11%** | 0,90 |

El juego A concede un descuento del 10% a 3.114 predios que pierden menos del 2% de
superficie útil: sobrecompensa por un factor de cinco, sobre el 26% del padrón. El
juego B asigna 10% donde la pérdida real es 8,11%.

**Los coeficientes de la propia norma validan el corte.** Agrupando por `nv` en 7 y
9, los deméritos medianos de las tres bandas son 0,75% / 8,11% / 24,85%, contra los
1,00 / 0,90 / 0,80 que la Guía tabula sin explicar su origen.

Residuos medidos:

| | Juego A | Juego B |
|---|---|---|
| sobrecompensados (descuento indebido) | **2.524** | — |
| subcompensados (sin descuento merecido) | — | **424** |
| de esos, graves (`rec` < 0,80) | — | 211 |

El juego A comete **seis veces más errores por conteo**. Se prefiere el error del
juego B por una asimetría de recurso: un propietario perjudicado puede pedir
revisión y el carril de QC existe para atenderlo; los sobrecompensados no reclaman
nunca y el municipio resigna recaudación sin enterarse.

`Ff` medio sin guarda ni QC: **0,9582** (juego A) contra **0,9887** (juego B).

### Confirmación visual

Inspección en el visor institucional. Las descripciones de forma son observacionales
y no fueron revalidadas por SQL.

| Predio | `nv` | `rec` | Superficie | Observación |
|---|---|---|---|---|
| `5-99-15` | 6 | 0,997 | 610,66 m² | Rectángulo alargado, indistinguible de sus vecinos del mismo loteo |
| `1-31-28` | 6 | 0,997 | 1.064,55 m² | Romboide alineado con la trama diagonal, normal para su manzana |
| `2-104-13` | 6 | 0,37 | 169,06 m² | Gancho en L encajado entre vecinos. Visiblemente deforme |
| `5-86-2` | 7 | 0,20 | 293,10 m² | Tira delgada con gancho. El peor de Uyuni |
| `2-147-1` | 3 | 0,5000 | 2.439,76 m² | Triángulo. Ver L3 |

Mismo conteo de vértices, formas opuestas: **el conteo por sí solo no discrimina y
la rectangularidad sí.** De ahí que la guarda y D2 no sean accesorios.

---

## Limitaciones declaradas

Ninguna se oculta. Van a la ordenanza municipal.

### L1 — Residuo silencioso: 83 predios (0,7%)

De los 424 subcompensados del juego B, **341 son interceptados por el carril de QC
y 83 no**. Esos 83 reciben `Ff` = 1,00 perdiendo más del 10% de superficie útil, y
ninguna guarda los detecta: son los de `rec` entre 0,80 y 0,90 con `sol` ≥ 0,95.
Cota superior de la clase: 213.

### L2 — La tolerancia es una palanca sobre la recaudación

Cuantificado en §2. Un municipio que quiera recaudar más elige 0,25; uno que quiera
complacer usa geometría cruda. Mismo criterio, mismos datos, distinto impuesto.
**Mitigación: la tolerancia se aprueba por ordenanza con la tabla de §2 adjunta.**

### L3 — El conteo de vértices no detecta lotes convexos oblicuos

Un triángulo es el polígono con menos vértices posibles y pierde la mitad de su
rectángulo envolvente. Hay **33 predios con `nv` ≤ 4 y `rec` < 0,80** que ningún
juego de cortes detecta; solo D2 los ve.

Caso verificado: **`2-147-1`**, triángulo de 2.439,76 m², `rec` 0,5000, `sol`
1,0000. Es la cuña residual que deja la Ruta Nacional 5 al cortar en diagonal la
trama ortogonal, sin edificar, barrio Miraflores.

**Decisión: estos 33 van a revisión manual, no reciben `Ff` = 0,80 por regla.**
Entre ellos habrá cuñas inservibles —descuento justificado— y lotes grandes con
frente sobre ruta nacional, comercialmente premium. Una regla no los distingue; un
técnico mirando el mapa sí. Son 33 revisiones. Es coherente con lo que la propia
Guía hace con la edificación singular: peritaje individual en lugar de fórmula
masiva (p. 40).

### L4 — `Ff` es prácticamente constante en Uyuni, y el trabajo real lo hace el QC

Esta es la limitación más importante y la menos evidente.

```
valuados con Ff = 1,00 : 10.920  de 11.040  =  98,9%
valuados con descuento :    120  de 11.040  =   1,1%
enviados a revision    :    945  de 11.985  =   7,9%
```

**El carril de QC es ocho veces más grande que las dos bandas de descuento
juntas.** Se diseñó como vía de excepción y resultó ser el resultado principal de
la regla.

Consecuencia operativa: **945 revisiones manuales son semanas de trabajo para un
GAM con dos o tres técnicos de catastro**, y sin ellas el 7,9% de la base imponible
no se puede liquidar. Hay que presupuestarlo.

Esto no es un fracaso de la métrica: **Uyuni es un loteo ortogonal regular y un
factor de forma debe ser casi constante en una ciudad así.** El problema es de
proporción entre la complejidad de la regla y su efecto. Ver Pendiente 1 y 2.

### L5 — Los umbrales son de Uyuni, no universales

Salen de una trama ortogonal levantada por el IGM. **Caranavi no pudo medirse**:
sus 6.573 predios están declarados `MULTIPOLYGON` y 11 tienen partes secundarias de
área ≈ 0,0000 —astillas de la conversión del DGN—, con un caso de 44 partes donde
la principal es 208,61 m² y el resto suma 0,21 m².

Consecuencia: **cada municipio nuevo requiere calibración de los seis parámetros
antes de poder valuar.** Es tarea de onboarding, no un valor por defecto.

---

## Precisión de la medición

`[V]` La desigualdad `sol ≥ rec` es exacta en aritmética real, pero la medición
encontró **94 casos con diferencia significativa, la peor de −0,000202** (0,02%). Es
la implementación de `ST_OrientedEnvelope`, no un contraejemplo geométrico. No mueve
ningún umbral. **Se registra para que ninguna regla se apoye en que la desigualdad
valga estrictamente.**

---

## Pendiente

1. **Evaluar `umbral_qc_solidez` = 0,90.**

   `[V]` Medido, distribución de solidez por debajo de 0,95:

   | Banda de `sol` | Predios | `rec` mediana de la banda |
   |---|---|---|
   | 0,90–0,95 | 258 | 0,8505 |
   | 0,85–0,90 | 186 | 0,7692 |
   | 0,80–0,85 | 118 | 0,6946 |
   | < 0,80 | 237 | 0,5615 |

   `[V]` Medido, efecto de mover el umbral:

   | Umbral | QC | Valuados (reg / irr / muy irr) | `Ff` medio | Salen del QC | Residuo |
   |---|---|---|---|---|---|
   | **0,95** | **945** | **10.920 / 81 / 39** | **0,9986** | — | **83** |
   | 0,90 | 700 | 11.044 / 157 / 84 | 0,9971 | 245 (124 a regular) | no medido |
   | 0,85 | 673 | 11.055 / 164 / 93 | 0,9969 | 272 (135 a regular) | no medido |

   **Motivo de no adoptar 0,90 ahora:** el residuo silencioso con ese umbral no
   está medido. Los 124 predios que pasarían a la banda regular podrían engrosarlo,
   pero **su rectangularidad no fue medida**: el 0,8505 de la tabla corresponde a
   los 258 de la banda completa, no a ese subconjunto. Se prefiere el umbral cuyo
   residuo está medido.

   `[C]` Interpretación de quien redacta, no medida: que la banda 0,90–0,95 tenga
   `rec` mediana 0,85 sugiere lotes reales con una muesca antes que defectos de
   digitalización, y el salto de 245 a 272 al bajar de 0,90 a 0,85 sugiere
   rendimiento marginal decreciente. Ninguna de las dos lecturas está verificada.

   Consultas necesarias para cerrarlo: residuo silencioso con `sol < 0,90` como D1,
   y distribución de `rec` de los 124 que cambian de carril.

2. **Evaluar la solidez como vía de demérito y no solo como disparador de QC.**

   Hipótesis: asignar `Ff` = 0,90 a `sol` 0,90–0,95 y `Ff` = 0,80 a `sol`
   0,80–0,90, dejando en QC solo `sol` < 0,80.

   `[C]` **Estimación no verificada** de quien redacta, obtenida sumando bandas y
   sin considerar la interacción con `nv`, la guarda ni D2: daría del orden de 680
   predios con descuento y 300 a revisión, contra los 120 y 945 actuales. **La
   cifra puede estar equivocada por un margen amplio y requiere consulta propia.**

   Si esa proporción se confirmara, invertiría la relación actual entre regla y
   excepción, que es la observación de L4. El costo sería de auditabilidad: la
   solidez no se cuenta a mano, y ese fue el argumento central para elegir el
   conteo de vértices (§1). Evaluar cuando haya un segundo municipio calibrado y se
   puedan comparar las dos tramas.

3. Calibrar los seis parámetros para Caranavi, previa resolución de los 11 predios
   con astillas.

4. Reflejar en M016: persistir `nv`, `rec` y `sol`; derivar `Ff` en cálculo;
   catálogo de los seis parámetros por municipio y versión de fórmula; y el carril
   de QC como estado del predio, no como columna calculada.
