# ADR-0069 — Modelo de datos de valuación

- **Estado:** Propuesta (borrador para revisión y aprobación de Saul).
- **Fecha:** 2026-07-31 · Fase 4.B.
- **Responde al** punto 3 de "Pendiente de cierre" de ADR-0066, que preguntaba si
  D5 y D6 justifican un ADR separado de modelo de datos. **Sí lo justifican.**
- **Precede a M016.** No se escribe migración hasta que este ADR esté aprobado.
- **Depende de:** ADR-0066 (fórmula y cadena), ADR-0067 (catálogo vacío),
  ADR-0068 (calibración de `Ff`).
- **Evidencia:** tareas 4.B.1 a 4.B.3 y dos consultas complementarias, todas de
  solo lectura sobre la versión activa de Uyuni (`051201`, 11.985 predios).
- **Auditoría:** tarea 4.B.4 reprodujo íntegramente las cifras `[V]` y corrigió
  nueve afirmaciones del borrador inicial. Las dos consultas que sostienen D5 —la
  distribución por proporción y el conteo de materiales por manzana— reprodujeron
  **exactamente**. Ver §Criterios de diseño para la separación entre lo medido y lo
  decidido.

---

## Contexto

ADR-0066 especificó la fórmula. ADR-0067 determinó que el catálogo de valores nace
vacío. ADR-0068 calibró `Ff`. Falta el modelo de datos, y las decisiones que
siguen **sobreviven a M016**: si quedan implícitas en una migración, quien abra el
esquema dentro de dos años las va a leer como accidentes de implementación y las va
a "corregir".

El reconocimiento estableció el punto de partida `[V]`:

- **No existe el esquema `valuacion`.** Los esquemas son `auditoria`, `dominio`,
  `fase3b_tmp`, `identidad`, `public`, `topology`.
- **No existe ningún concepto de gestión, ejercicio o vigencia.** Cero columnas.
- Las migraciones son **EF Core, M001–M015**, con `migrationBuilder` fluido más
  `migrationBuilder.Sql` para triggers y datos. Historial en
  `identidad.__ef_migrations_history`, no en la tabla estándar.
- El patrón de inmutabilidad por trigger `BEFORE UPDATE/DELETE` está probado en
  **diez tablas**: las nueve `capa_*` y `auditoria`. Son 10 triggers, cada uno con
  eventos de `UPDATE` y `DELETE`, o sea 20 filas de evento. (Las otras 2 filas del
  catálogo pertenecen a `topology.layer` y no son de inmutabilidad.)
- La PK de `dominio.predios` es `id` uuid. La llave catastral es un **índice único
  filtrado de cuatro columnas**, no una restricción declarada:
  `uix_predios_municipio_triplete_activo` sobre
  `(municipio_codigo, cod_uv, cod_man, cod_pred) WHERE NOT is_deleted`. La tripleta
  es su parte catastral; el índice completo incluye el municipio.
- `dominio.equivalencias_valor` **pertenece al pipeline de importación**
  (`mapeo_columna_id`, `valor_origen`, `valor_destino`), no a valuación. Está vacía
  y no interfiere.

---

## Decisiones

### D1 — Esquema `valuacion`, separado de `dominio`

Contextos distintos con ciclos de vida distintos, y permisos que difieren: un
usuario de recaudaciones debe leer valuación sin poder escribir cartografía. D1 de
ADR-0066 ya nombró `valuacion.formula_version` como concepto.

**Decisión:** se crea el esquema `valuacion`. `dominio` no se modifica salvo por
claves foráneas entrantes.

### D2 — La `corrida` fija cuatro ejes ortogonales

Un avalúo depende de cuatro cosas independientes entre sí:

```
dataset_version_id   -> que cartografia
parametros_version   -> que ordenanza municipal
formula_version      -> que capitulo normativo (Cap. IV o Cap. VI, ADR-0066 D2)
gestion              -> que ano fiscal
```

**Decisión:** existe `valuacion.corrida` que fija los cuatro y a la que pertenecen
los avalúos, **con máquina de estados análoga a `DatasetVersion`**: `EnCalculo →
PreviewListo → Emitida → Archivada`, más `Fallida` y `Descartada`.

Se reusa el **patrón conceptual** de ADR-0049 en lugar de inventar uno: el equipo
ya lo conoce y la semántica de snapshot está probada.

`dominio.historial_estados` **no es reutilizable como tabla**: su FK es
`predio_id → dominio.predios.id` y no puede registrar estados de una corrida. Se
reusa la forma, no la estructura.

`gestion` es un entero de cuatro dígitos. Es el primer lugar del sistema donde
aparece el concepto; no hay nada que reusar.

### D3 — El avalúo emitido es inmutable y autocontiene su memoria de cálculo

Un contribuyente impugna en 2029 una liquidación de 2026. Hay que reproducirla
exactamente, aunque desde entonces cambiaron la cartografía, los coeficientes por
ordenanza y hasta el capítulo rector.

**Decisión:** el avalúo emitido guarda **todos los insumos y todos los factores con
su valor aplicado**, no referencias que puedan resolverse distinto en el futuro:

```
SupT aplicada, Mv aplicado, y cada coeficiente con su valor:
    Fs, Fi, Ff, Fum
el eslabon zonal completo:  VPz, VSz, IPIU, IPES, IPRT, IPV
la tripleta (cod_uv, cod_man, cod_pred) copiada como llave de negocio
el origen de Mv: directo, mayor IPV, o heredado de manzana
```

**Trigger de inmutabilidad `BEFORE UPDATE/DELETE`** una vez que la corrida pasa a
`Emitida`, con el mismo mecanismo de las diez tablas existentes. Antes de emitir, el
avalúo es recalculable.

`[C]` **La semántica condicional es nueva.** Los diez triggers actuales bloquean de
forma incondicional; condicionar el bloqueo al estado `Emitida` no está probado en
el sistema. Requiere diseño y prueba explícitos en M016, no es copiar un patrón.

La referencia a `predios.id` se conserva para navegación, pero **la tripleta va
copiada**: si el predio se subdivide o fusiona, el avalúo histórico sigue diciendo
a qué se refería.

### D4 — Métricas geométricas por `(dataset_version, predio, tolerancia)`

ADR-0068 estableció almacenar la medición y derivar el factor. Con una precisión
que no es obvia: **`nv` no es función pura de la geometría**, es función de la
geometría **y de la tolerancia de normalización**, que es parámetro municipal
versionado.

**Decisión:** `valuacion.metrica_geometrica` se llavea por
`(dataset_version_id, predio_id, tolerancia)`. Persiste `nv`, `rec` y `sol`; `Ff` se
deriva en cálculo.

Si se materializara solo por predio, el día que una ordenanza mueva la tolerancia
habría métricas mentirosas sin que nada avise.

### D5 — Asignación predio→vía: umbral 15 m y tres vías de excepción

`Mv` es el multiplicando de `Vt` y no existe clave entre `predios` y `capa_vias`:
la asignación es espacial y falible. La Guía prescribe individualización por tramo
de manzana (pp. 22 y 28), y `capa_vias` **no tiene referencia a manzana**, así que
la asignación no puede hacerse por código.

**Umbral: 15 m**, parámetro municipal versionado.

`[V]` Distancia mediana del predio a la vía más cercana: **8,53 m**; p90 16,03 m;
p99 48,70 m; máximo 163,67 m.

`[C]` Caracterización de la fuente e interpretación de quien redacta, no medidas:
`capa_vias` **representaría ejes de vía y no calzadas** —ninguna consulta lo
demuestra—, y bajo esa lectura una mediana de 8,53 m correspondería a media calzada
de calles del orden de 15–17 m. Esa interpretación es la que explica por qué el
umbral de 5 m usado en el reconocimiento inicial capturó solo 1.611 predios.

`[V]` Rendimiento marginal medido:

| Umbral | Cobertura total | Asignación inequívoca | Ambiguos | Ganancia inequívoca | Costo en ambigüedad |
|---|---|---|---|---|---|
| 10 m | 56,1% | 54,7% | 169 | — | — |
| **15 m** | **85,7%** | **83,2%** | **300** | **+3.423** | **+131** |
| 25 m | 97,4% | 92,4% | 595 | +1.106 | +295 |

De 10 a 15 m se ganan **26,1 predios de asignación por cada uno que se vuelve
ambiguo**; de 15 a 25 m, **3,75**. El rendimiento cae siete veces. Además 15 m tiene
sentido físico: media calzada de una calle de 20 m más margen; a 25 m se capturan
vías del otro lado de la manzana, que no dan acceso al predio.

**Clasificación proyectada de los 11.985 predios de Uyuni.** Los conteos son `[V]`;
su conversión en asignación automática es **consecuencia de adoptar las reglas de
este ADR**, no un resultado medido de una ejecución:

| Clase | Predios `[V]` | % | Destino bajo las reglas de D5 |
|---|---|---|---|
| Un solo material a 15 m | 9.973 | 83,2% | asignación directa |
| Dos o más materiales a 15 m | 300 | 2,5% | mayor `IPV` · 285 con dos, 15 con tres o más |
| Sin vía; manzana con un solo material observado | 1.443 | 12,0% | herencia |
| **Subtotal automatizable** | **11.716** | **97,8%** | |
| Sin vía; manzana con dos o más materiales | 157 | 1,3% | QC · 139 con dos, 18 con tres |
| Sin vía; manzana sin ningún material observado | 112 | 0,9% | 12 manzanas, deficiencia declarada |

**Regla de ambigüedad:** se toma el material de **mayor `IPV`**. Cubre por igual los
285 casos de dos materiales y los 15 de tres o más.

`[C]` **Es un criterio, no una consecuencia.** La geometría no mide por dónde se
accede al inmueble; la norma admite tanto "colindan" como "por donde se accede"
(p. 36) y un lote de esquina cumple ambas con dos vías distintas. La elección
contraria —tomar el menor `IPV`— sería igualmente compatible con el texto. Ver
§Criterios de diseño para el fundamento y su contraargumento.

**Regla de herencia:** un predio sin vía a 15 m hereda el material de su manzana
**si y solo si** las vías que rodean a esa manzana tienen un único material. Con
dos o más, va a QC: elegir sería arbitrario.

Fundamento de la herencia `[V]`: de las 348 manzanas con predios sin vía, **294
tienen un único material observado a 15 m de alguno de sus predios**. Esa es la
operación exacta que se midió; **no se recorrió el perímetro de `capa_manzanas`**,
así que "rodeada por un solo material" es una abreviatura, no una verificación
perimetral.

`[V]` Distribución por proporción de predios sin vía dentro de cada manzana:

| Proporción sin vía en la manzana | Manzanas | Predios sin vía |
|---|---|---|
| < 20% | 156 | 359 |
| 20–40% | 112 | 626 |
| 40–60% | 56 | 498 |
| 60–80% | 9 | 82 |
| ≥ 80% | 15 | 147 |

`[C]` **Lectura de quien redacta, no medida.** Una manzana está rodeada por cuatro
calles; si falta una en `capa_vias`, alrededor del 25% de sus predios pierde frente.
Bajo esa lectura, los 1.353 predios de las bandas ≥20% —el 79%— estarían en manzanas
con cobertura vial incompleta antes que en posición interior. **No se midió cuántas
calles perimetrales faltan ni la causa.** Una medición directa exigiría comparar el
perímetro de `capa_manzanas` contra `capa_vias`.

**Toda asignación registra su método**, y el avalúo lo copia (D3). Un `Mv`
heredado es visible, auditable y reclamable; uno directo también.

**Toda asignación es corregible por un humano**, con registro de quién y cuándo.
La norma dice que `Mv` sale de la vía *"por donde se accede al inmueble"* (p. 36), y
**la geometría sola no puede determinar cuál es la vía de acceso** en un lote de
esquina. Si se resuelve solo por regla, no hay forma de atender una impugnación
legítima.

### D6 — El catálogo de coeficientes tiene vigencia

No basta versionar por municipio: una ordenanza los cambia **a partir de una
gestión determinada**.

**Decisión:** `valuacion.parametros_version` se llavea por
`(municipio_codigo, gestion_desde)`, con `gestion_hasta` nullable para la vigente.
Contiene los coeficientes tabulados de RM 024/2024 —`IPIU`, `IPES`, `IPRT`, `IPV`,
`Fs`, `Fi`, `Ff`, `Fum`— y los **siete parámetros escalares**:

| Parámetro | Uyuni | Origen |
|---|---|---|
| `tolerancia_normalizacion` | 0,05 m | ADR-0068 |
| `corte_regular` | 7 | ADR-0068 |
| `corte_irregular` | 9 | ADR-0068 |
| `umbral_guarda_rec` | 0,95 | ADR-0068 |
| `umbral_qc_solidez` | 0,95 | ADR-0068 |
| `umbral_qc_rect` | 0,80 | ADR-0068 |
| `umbral_asignacion_via` | **15 m** | este ADR, D5 |

### D7 — El carril de QC es un flujo de trabajo, no una bandera

Son 945 predios por `Ff` (ADR-0068) más 157 por herencia ambigua. **La superposición
entre ambos conjuntos no fue medida**, así que el total no es la suma.

Un predio en QC tiene ciclo de vida: detectado → asignado → resuelto. Y las
resoluciones son distintas: valuado a mano, geometría corregida, `Mv` asignado
manualmente, excluido con motivo.

**Decisión:** `valuacion.observacion` con `motivo` tipificado, estado, responsable y
resolución. **Un `boolean` no alcanza.**

Motivos tipificados:

```
FF_SOLIDEZ_BAJA          FF_CONTRADICCION_METRICAS
MV_HERENCIA_AMBIGUA      MV_SIN_COBERTURA_VIAL
GEOMETRIA_ANOMALA        MANZANA_SIN_REGISTRO
```

`MV_SIN_COBERTURA_VIAL` **no es un problema del predio**: es una deficiencia de
`capa_vias` que se resuelve completando cartografía y recalculando, no revisando
predio por predio. Se tipifica aparte para que su dueño sea el equipo cartográfico
y no el técnico de catastro.

### D8 — El catálogo vacío se valida antes de emitir, no durante

ADR-0067 lo hace nacer vacío. **Decisión:** la corrida verifica en el paso a
`PreviewListo` que existan todas las entradas necesarias, y si faltan **falla con la
lista exacta de lo que falta**. Mismo patrón que las validaciones bloqueantes B1–B4
del preview de importación (ADR-0049).

Sin esta validación, una corrida con catálogo incompleto produciría avalúos en cero
sin avisar. Es el peor modo de falla posible en un instrumento fiscal.

---

## Criterios de diseño, no consecuencias de la medición

La evidencia cuantifica **factibilidad y alcance**. No determina por sí sola ninguna
de las decisiones de este ADR. Se listan para que un lector futuro no las confunda
con deducciones, y para que quien quiera cambiarlas sepa que puede hacerlo sin
contradecir ningún dato.

**Criterios arquitectónicos** — D1 separar `valuacion` de `dominio` · D2 los estados
y transiciones de `corrida` · D3 avalúo autocontenido e inmutable tras emitir ·
D4 la llave de `metrica_geometrica` · D6 llave y vigencia de `parametros_version` ·
D7 modelar QC como flujo de trabajo · D8 bloquear `PreviewListo` con catálogo
incompleto.

**Criterios operativos de D5** — el umbral de 15 m frente a 10 o 25 · resolver la
ambigüedad por mayor `IPV` · heredar el material dentro de la manzana · clasificar
las 12 manzanas como deficiencia cartográfica y no como problema del predio ·
permitir corrección humana de toda asignación.

Tres merecen su fundamento explícito, porque son las más discutibles:

**El umbral de 15 m.** Lo que la medición aporta es el rendimiento marginal: 26,1
predios asignados por ambiguo entre 10 y 15 m, contra 3,75 entre 15 y 25 m. La
elección del punto de quiebre es criterio; otro municipio con calles más anchas
podría fijarlo en 20 m sin contradecir nada.

**El mayor `IPV` para los ambiguos.** La Guía dice que `Mv` sale de la vía *"al cual
colindan o por donde se accede al inmueble"* (p. 36). Un lote de esquina **colinda
con las dos**, y la geometría no puede determinar por cuál se accede. Elegir la
mejor es consistente con que la propia norma trate la esquina como ventaja: `Fum`
paga 1,20. Pero es una elección, y la contraria —tomar la vía de menor `IPV`— sería
igualmente compatible con el texto. Por eso D5 exige que la asignación sea
**corregible por un humano con registro**: sin esa válvula, el criterio sería
indefendible ante una impugnación.

**La herencia dentro de la manzana.** Quien redacta este ADR sostuvo primero la
posición contraria —que heredar sería inventar dato— y cambió al ver la distribución
por proporción. La medición no obliga a heredar: obliga a reconocer que 1.443
predios están en manzanas donde solo se observó un material, y que mandarlos a
revisión manual junto con los 157 realmente ambiguos sería tratar igual dos
situaciones distintas. La decisión de heredar es criterio, y su mitigación es que el
avalúo declare el origen como *heredado* (D3), de modo que sea visible y reclamable.


## Consecuencias

- **`Mv` queda asignable automáticamente al 97,8% de los predios de Uyuni** bajo
  las reglas de D5, con 2,2% en vías de excepción tipificadas.
  **Esto no significa que la Fase A quede calculable al 97,8%**: el catálogo de
  valores nace vacío (ADR-0067), y existe además el carril de QC de `Ff` con 945
  predios (ADR-0068) cuya intersección con los 157 de `Mv` no fue medida. La Fase A
  queda calculable cuando exista campaña de encuestas; lo que este ADR resuelve es
  que **`Mv` no agrega una tercera dependencia externa**.
- **`Mv` no es insumo municipal.** Se calcula. Eso mantiene en dos las dependencias
  externas del GAM declaradas en ADR-0067, sin agregar una tercera.
- M016 se escribe copiando el estilo de M015: `migrationBuilder` fluido,
  `migrationBuilder.Sql` para triggers, `geometry(Geometry,32719)` de
  NetTopologySuite, y `Down` con guarda contra pérdida de datos.
- Los triggers de inmutabilidad de ADR-0044 **no se tocan**. Los nuevos siguen su
  patrón sin modificarlo.

---

## Limitaciones declaradas

### L1 — La superposición de los carriles de QC no está medida

945 predios por `Ff` y 157 por `Mv`. El total real está entre 945 y 1.102. Debe
medirse antes de dimensionar el trabajo del GAM.

### L2 — Trece tramos de 300 m o más

`[V]` Medido: 13 tramos de ≥300 m. Cuatro son `CARRETERA` y uno `ROTONDA`; ocho son
`CALLE`, el mayor `CALLE A` con 701,64 m. Cada feature declara **un único valor de
`material`**: siete tierra y una adoquín entre las calles. Existen dos registros
denominados `CALLE 27`.

`[C]` Lectura de quien redacta: las carreteras y la rotonda son plausiblemente
largas por naturaleza, y las ocho calles probablemente cruzan varias manzanas sin
individualizar. **Nada de eso fue medido.** Y sobre todo: que cada feature declare
un material único **no demuestra que el material físico sea uniforme a lo largo de
toda la traza** — solo que el atributo lo es.

**Riesgo declarado:** si una calle larga cambia de pavimento en su recorrido, todos
los predios que la enfrentan reciben el mismo `Mv` y algunos estarán mal. En Uyuni
el impacto es acotado porque el 90% de las vías es tierra. **Antes de un municipio
con más pavimento hay que verificar la uniformidad física**, contrastando la traza
contra ortofoto o levantamiento.

### L3 — Veinticuatro vías con material y sin geometría

`[V]` 24 filas de `capa_vias` tienen material informado y `geometria` nula, con tres
materiales distintos. Es información que existe y no se puede ubicar. Deuda de dato
de la fase 1.

Confirmado además que **no existe el caso inverso**: cero vías con geometría y
material nulo.

### L4 — Un predio con manzana inexistente en la capa

`[V]` `sin_manzana_en_capa = 1` sobre 685 combinaciones declaradas. Reproduce de
forma independiente lo que ADR-0045 ya había anotado: 685 combinaciones en predios
contra 684 filas en `MAN_SIS_UYU`.

### L5 — Todo esto es Uyuni

Los siete parámetros y las tres reglas de excepción salen de una trama ortogonal
levantada por el IGM. **Caranavi no pudo medirse** por sus 11 predios con astillas
(ADR-0068 L5). Cada municipio nuevo requiere calibración antes de valuar.

---

## Pendiente

1. **Medir la superposición de los carriles de QC** (L1). Una consulta.
2. `[C]` Evaluar si `valuacion.metrica_geometrica` conviene materializada o como
   vista. La materialización es correcta si el recálculo es caro; no se midió el
   costo de calcular `nv`, `rec` y `sol` sobre 11.985 predios.
3. **Defecto de herramienta, fuera del alcance de este ADR pero bloqueante para
   automatizar:** `scripts/sql.ps1` **no propaga el código de salida de psql**. Una
   consulta que falla reporta `SQL_EXIT_CODE=0`. Verificado en la tarea 4.B.3, donde
   un error de `GROUP BY` devolvió éxito. Todo `SQL_EXIT_CODE=0` de la fase 4 fue
   vacío como verificación; lo que salvó el trabajo fue el protocolo de salida
   literal. **Cualquier script de valuación masiva que confíe en ese código estaría
   ciego.** Merece corrección propia.
4. `fase3b_tmp` con once tablas y una vista sigue viva en la base tras sellar la
   fase 3.B con tag. Andamiaje no documentado en ningún ADR.
5. **Verificar la uniformidad física del material** en las ocho calles de ≥300 m
   (L2), contrastando la traza contra ortofoto. Bloqueante antes de un municipio con
   más pavimento que Uyuni.
6. **Determinar si las 12 manzanas sin vías a 15 m son omisión cartográfica,
   condición urbana real o mezcla.** Este ADR las clasifica como deficiencia; esa
   clasificación es criterio y no fue verificada en terreno ni contra ortofoto.
7. **Identificar el predio** cuya combinación `(cod_uv, cod_man)` no existe en la
   capa activa de manzanas (L4).
8. Diagnóstico de `DATOS_VILLA_LOZA.zip` y la planimetría DWG 2025 de Caranavi,
   recibidos durante esta fase. Entran como versión nueva de dataset por el pipeline
   de importación, no como parche.
