# ADR-0071 — Separación entre norma nacional y decisión municipal: aprobación, vigencia y divergencia

- **Estado:** Aceptada.
- **Versión:** 8, promovida tras seis dictámenes de auditoría documental y seis
  pasadas sobre el modelo ejecutable. `[C]` Relato de proceso, no hecho auditable:
  solo la versión 1 está commiteada (`1c89d3f`); las intermedias existieron
  únicamente en el árbol de trabajo.
- **Fecha:** 2026-08-05 · Fase 4.B.
- **Completa** D2 y D6 de ADR-0069.
- **Corrige parcialmente** ADR-0066 D6 (§D1).
- **Corrige la terminología** de ADR-0068, ADR-0069 y ADR-0070 (§D8).
- **Precede a M016**, lo autoriza a modificar `dominio` en un punto acotado
  (§D11) y le impone cuatro obligaciones explícitas (§Frontera).
- **Naturaleza:** decisión de diseño. Salvo donde se indique, su contenido no se
  deriva de ninguna medición. Se marca `[C]` por defecto.

### Evidencia ejecutable

```
scripts/modelo_vigencia_adr0071.py
commit  f1273ff
SHA-256 281a94e1ec430acd2ed087ea20983922d0353afe75f79069534edccbbec8b95d
128.445 bytes · 2.925 lineas · solo biblioteca estandar
162 comprobaciones · 38 mutantes · 1 control de redundancia
```

Especificación de referencia **provisional y no productiva**. **Falsa la
coherencia interna** de la parte del ADR que cubre: termina con código distinto de
cero ante cualquier contradicción. **No determina legalidad, no prevalece sobre
este ADR y no es fuente normativa.** No entra en la compilación ni en la suite de
pruebas.

| Cubre | No cubre |
|---|---|
| Los predicados de §D3 y su tabla de casos | La máquina de transiciones de §D2 |
| Los gates de §D4 y §D12 | El historial de transiciones y sus fechas |
| Las **invariantes de sucesión** de §D2 | La validación del objetivo de `APRUEBA_PARAMETRO` como código concreto |
| La validación en dos niveles de §D11 | La semántica propia del rol `REFERENCIA` |
| La monotonía y la identidad de la corrida | Toda persistencia |

`[C]` Que el modelo no cubra algo **no autoriza a M016 a omitirlo**: el ADR
describe el sistema, el modelo falsa una parte de él. La columna derecha se
implementa sin respaldo ejecutable y merece más cuidado, no menos.

`[V]` Las cifras `[V]` **de base de datos** de este documento fueron
re-verificadas por consulta directa el 5 de agosto de 2026, con salida literal,
sobre la versión activa de Uyuni `b6934919-62fa-40ed-b557-d94a01cd9d65`. Las
cifras de cobertura de L7 **no** provienen de esa consulta sino del modelo
ejecutable, y allí se declara su procedencia una por una.

`.gitattributes` fija `*.py text eol=lf`, porque `core.autocrlf` está activo y un
checkout escribiría CRLF, cambiando el hash que este ADR cita como ancla. La misma
regla preserva el de `scripts/auditoria_vz.py`, citado en ADR-0066 y ADR-0067.

---

## Frontera entre este ADR y la especificación de M016

`[C]` Las revisiones sucesivas de este documento crecieron hacia el detalle de
esquema y cada ronda de auditoría abría detalles nuevos sin converger. La frontera
se declara y se mantiene.

`[C]` **Solo la versión 1 está commiteada** (`1c89d3f`); las versiones 2, 3 y 4
existieron únicamente en el árbol de trabajo. Toda afirmación de este documento
sobre el contenido de esas versiones intermedias **no es auditable desde el
repositorio** y debe leerse como relato del proceso, no como hecho verificable.

**El ADR decide el *qué* observable:** estados y transiciones, condiciones de
vigencia y aplicabilidad, precedencia entre cese, reemplazo e instrumentos,
cardinalidades, semántica `AND`/`OR`, gates, contenido mínimo que una corrida debe
congelar, y la corrección a ADR-0066 D6.

**La especificación de M016 decide el *cómo* físico.** Quedan como **obligaciones
explícitas de la especificación y gate previo a generar la migración**:

| # | Obligación de la especificación |
|---|---|
| **E-1** | Tablas de vínculo, roles, objetivos y grupos que realicen la semántica de §D4 |
| **E-2** | Tabla del snapshot `corrida – grupo – instrumento` de §D12 |
| **E-3** | Representación de la compatibilidad `parametros_version ↔ formula_version` y su cardinalidad |
| **E-4** | Nombres de restricciones e índices, `RESTRICT` frente a `NO ACTION`, y diferibilidad — **siempre que no admitan un estado inválido confirmado ni borrado en cascada** |

La especificación asigna además los **códigos estables** de los dos catálogos del
sistema; este ADR fija su contenido, no su codificación.

`[C]` La quinta obligación que la tercera auditoría propuso —convertir la tabla
temporal en casos ejecutables— **queda cumplida por el modelo ejecutable**, no por
la especificación.

---

## Contexto

Los ADR anteriores calibraron parámetros sobre los datos de Uyuni y los declararon
*propuesta técnica pendiente de ordenanza*. **El sistema no se construye para
Uyuni**, sino para los Gobiernos Autónomos Municipales de Bolivia; Uyuni y Caranavi
son los dos primeros casos. La pregunta correcta no es *qué valores adopta Uyuni*
sino **qué le corresponde decidir al sistema y qué a cada GAM**.

`[V]` Se buscó normativa municipal con once patrones de nombre en las tres raíces
del entorno de trabajo. **No se localizó ningún instrumento normativo municipal con
esos patrones**; `docs/normativa/` contiene únicamente un `.gitkeep` de 0 bytes. La
medición respalda *no localizado*, no inexistencia absoluta.

`[D]` Se ha reportado la existencia de una Ley Autonómica Municipal N.º 090/2024 de
Zonificación y Valuación Zonal de Uyuni, un Decreto Municipal 23/2025 que la
reglamenta y una Resolución Administrativa Municipal 48/2025, con una tabla CVZ
aplicada al IMPBI 2024. **Ninguno fue examinado**, y la afirmación no puede operar
como demostración mientras la fuente no se examine.

---

## Decisiones

### D1 — Frontera entre norma nacional y decisión municipal · **corrige ADR-0066 D6**

**El sistema implementa la norma nacional. El GAM fija los valores.**

ADR-0066 D6 decidió que **todos** los coeficientes son datos versionados por
municipio y por versión de fórmula. **Este ADR corrige parcialmente esa decisión**,
con el mismo mecanismo con que ADR-0070 corrigió las dos erratas de ADR-0066 D7.

| Elemento | Naturaleza |
|---|---|
| Algoritmo de cada capítulo | definición versionada y controlada por el sistema |
| Tablas normativas de cada capítulo | definición versionada y controlada por el sistema |
| Selección del capítulo aplicable | **dato municipal trazable en la corrida** |
| Valores y umbrales expresamente delegados al GAM | **datos municipales versionados** |
| Mapeo del vocabulario municipal | **dato municipal versionado** |
| Toda cifra monetaria | **dato municipal** |

`[C]` **Lo corregido de ADR-0066 D6 es que las tablas normativas sean datos
municipales editables.** Su versionado sigue existiendo; lo que cambia es quién las
edita. **La corrección no obliga a compilarlas en código:** la especificación puede
materializarlas como catálogo del sistema inmutable, cuyo contenido no proviene de
ningún GAM.

**Una modificación municipal de una tabla normativa no edita la fórmula existente:
exige otra `formula_version`** (§D10). Ese es el mecanismo por el cual la
delegación que ADR-0066 D6 quería preservar sigue disponible, sin que un municipio
pueda alterar el contenido de una fórmula ya aplicada a avalúos emitidos.

**Corolario 1 — Los parámetros `[C]` no son propuestas para Uyuni.** Son **valores
por defecto que el sistema deriva de los datos del propio municipio** cuando el GAM
no ha fijado los suyos.

| Origen | Cantidad | Detalle |
|---|---|---|
| ADR-0069 D6 | 7 | seis derivados de ADR-0068, uno de ADR-0069 D5 |
| ADR-0070 | 8 | seis marcados `[C]`, dos sin marca |

`[V]` Son **quince posiciones de catálogo, no quince parámetros calibrados**: dos
carecen de valor. `minimo_observaciones_zonal` está *por fijar* e
`ipv_material_lad` *sin asimilar*.

**Corolario 2 — No todo parámetro admite valor por defecto.** La línea es si exige
un juicio monetario o información de mercado externa:

- **Derivables**: umbrales de forma, tolerancia de normalización, umbral de
  presencia zonal, umbral de asignación a vía, reglas de agregación. El sistema
  calcula un valor por defecto sobre los datos del municipio y lo declara como tal.
- **No derivables**: valor zonal `Vz`, valores por m² de construcción `Tip`,
  cualquier cifra monetaria. **Nacen vacíos y ningún valor por defecto es
  admisible.** Ratifica ADR-0067 como comportamiento del producto y no como
  limitación local.

**Corolario 3.** `[C]` **Hipótesis de diseño, no observación nacional:** el desvío
de fuente de ADR-0070 D2 no es que a Uyuni le falte un dato, sino que la norma
exige un atributo que **probablemente** el catastro urbano boliviano típico no
registra. Se midió en un municipio y se extrapola; se confirma o se refuta con cada
incorporación nueva.

### D2 — Cinco estados, transiciones e invariantes de sucesión

Se almacenan **`PropuestaTecnica`**, **`Aprobada`**, **`Reemplazada`**,
**`Cesada`** y **`Descartada`**. El vocabulario es **cerrado**: un estado fuera de
esos cinco quedaría fuera de todos los gates y pasaría inadvertido, de modo que
invalida la configuración.

```
PropuestaTecnica  ->  Aprobada
PropuestaTecnica  ->  Descartada
Aprobada          ->  Reemplazada
Aprobada          ->  Cesada
```

| Estado | Significado | Exige |
|---|---|---|
| `Descartada` | propuesta que **nunca produjo efectos** | nada |
| `Reemplazada` | dejó de producir efectos **porque otra la sucede** | sucesora y `fecha_inicio_reemplazo` determinada |
| `Cesada` | dejó de producir efectos **sin sucesora** | acto de cese y `fecha_efecto_cese` |

`Reemplazada`, `Cesada` y `Descartada` son terminales. Ninguna transición es
reversible.

**`fecha_aprobacion`** de una versión es el **`ocurrido_at` de su transición a
`Aprobada`**, no el `registrado_at` ni ninguna fecha de instrumento.

**`fecha_inicio_reemplazo`** es el **primer día en que su sucesora puede satisfacer
`VigentePara` por sí misma**, sin considerar el cierre de la antecesora:

```
fecha_inicio_reemplazo = MAX(
    inicio_normativo_resuelto_de_la_sucesora,
    fecha_aprobacion_de_la_sucesora + 1 dia
)
```

El `+1 día` proviene de que la comparación de aprobación es estricta (§D3).

`[C]` **La antecesora solo pasa a `Reemplazada` cuando esa fecha está
determinada**, y **la sucesora debe poder producir efectos en ella**: no basta
calcularla. Una sucesora ya cesada, ya reemplazada, o cuya ventana normativa no
cubra esa fecha, invalida la sucesión.

**Invariantes de sucesión**, todas verificables en la base:

- la sucesora pertenece al **mismo municipio**;
- la sucesora es **distinta** de la reemplazada;
- la sucesora está en `Aprobada`, `Reemplazada` o `Cesada` — nunca en
  `PropuestaTecnica` ni `Descartada`;
- la relación de reemplazo **no admite ciclos de ninguna longitud**, incluido el de
  un solo nodo;
- **una sucesora no puede suceder a más de una antecesora**;
- `Reemplazada` implica sucesora no nula; `Cesada` y `Descartada` implican sucesora
  nula.

`[C]` **Por qué se rechaza la convergencia.** Dos antecesoras apuntando a la misma
sucesora hacen ambiguo su relevo: la sucesora tiene un único `inicio_efectivo`, y
dos `fecha_inicio_reemplazo` distintas apuntando a él describirían dos historias
incompatibles.

**Causas del cese.** `[C]` Derogación, anulación, suspensión y demás se registran
como **causa y fundamento del cese**, no como estados propios. Convertir cada causa
jurídica en un estado multiplicaría la máquina y obligaría al equipo técnico a
calificar jurídicamente.

**Suspensión levantada — modelado deliberado.** `[C]` Si una suspensión se levanta,
**se crea una `parametros_version` nueva**, aunque sus valores sean idénticos. La
nueva conserva como antecedente la cesada y registra el acto que restablece la
aplicabilidad. El resultado es un intervalo temporal explícito:

```
version A aprobada  ->  version A cesada  ->  intervalo sin aplicacion  ->  version B aprobada
```

**No se reactiva A ni se reescribe su historia.** El sistema no decide si
jurídicamente revive el mismo acto; esa calificación corresponde al GAM.

#### Registro de transiciones

Cada transición deja un asiento **append-only** con, al menos:

```
secuencia             entero, estrictamente creciente por version,
                      empieza en 1, sin huecos ni reutilizacion
estado_anterior       nulo UNICAMENTE en el asiento de creacion
estado_nuevo
ocurrido_at           cuando ocurrio el hecho juridico
registrado_at         cuando se cargo en el sistema
actor_tipo            PERSONA | PROCESO
actor_id              obligatorio; identifica a la persona o al proceso
causa                 obligatoria en Cesada; opcional en las demas
```

**El orden del historial es el de `secuencia`, no el de ninguna fecha.**
`(parametros_version_id, secuencia)` es único; el asiento de creación lleva
`secuencia = 1`; y **"el último asiento" es el de mayor `secuencia`**, sin empates
posibles.

`[C]` **Por qué no se ordena por fecha.** Ni `registrado_at` ni `ocurrido_at`
sirven como orden:

- Dos asientos pueden compartir `registrado_at` —una carga que registra dos
  transiciones en la misma transacción— y el orden quedaría indefinido.
- `ocurrido_at` **no es monótono**: una aprobación puede ocurrir el 3 de marzo y
  cargarse el 20 de abril, después de un asiento cuyo hecho ocurrió más tarde. El
  historial ordenado por hecho jurídico no describiría la sucesión de estados.

`[C]` La `secuencia` también hace verificable la **completitud** del historial: sin
huecos ni reutilización, un asiento faltante es detectable. Una fecha no da esa
propiedad.

**El asiento de creación.** Toda versión nace con **un asiento cuyo
`estado_anterior` es nulo y cuyo `estado_nuevo` es `PropuestaTecnica`**. Es el
único asiento que admite `estado_anterior` nulo, y el único cuyo `estado_nuevo` es
ese. Su `ocurrido_at` es la fecha en que la propuesta técnica quedó formulada.

`[C]` Sin este asiento, una versión existiría sin historia y el historial no sería
reconstruible desde su origen. Con él, **el estado actual de una versión es
derivable del asiento de mayor `secuencia`**, y no hace falta confiar en que la
columna de estado y el historial coincidan: cuando discrepan, **el historial
gobierna**, porque es append-only y la columna no.

El `ocurrido_at` del asiento de creación es la fecha en que la propuesta técnica
quedó formulada. `[C]` No participa de ningún predicado de §D3 —solo el de la
transición a `Aprobada` lo hace— y su precisión es de día, como todos los hechos
jurídicos de este ADR.

**El actor es obligatorio y siempre identificable.** `[C]` Una transición puede
originarse en una persona o en un proceso automático —por ejemplo, el que marca
`PreviewListo` al terminar una corrida—. En ambos casos hay un responsable
registrable: la persona, o el proceso con su identificador. **`actor_id` nunca es
nulo**; lo que varía es `actor_tipo`. Admitir un actor nulo haría que existieran
transiciones sin responsable, que es exactamente lo que un historial existe para
impedir.

**Qué causa qué.** `[C]` La `causa` del asiento y los campos que §D2 exige a la
versión son cosas distintas y no se sustituyen:

| Transición | Exige la versión (§D2) | Exige el asiento |
|---|---|---|
| a `Aprobada` | nada adicional | actor y fechas |
| a `Reemplazada` | sucesora y `fecha_inicio_reemplazo` | actor y fechas; causa opcional |
| a `Cesada` | acto de cese y `fecha_efecto_cese` | actor, fechas y **causa obligatoria** |
| a `Descartada` | nada | actor y fechas; causa opcional |

En `Cesada`, el **acto de cese** identifica el instrumento o la resolución que lo
produce; la **causa** califica el motivo —derogación, anulación, suspensión— sin
crear un estado propio (§D2). No son redundantes: uno es el documento, la otra es
la razón.

`[C]` **`ocurrido_at` y `registrado_at` son distintos a propósito.** Una ley se
sanciona el 3 de marzo y alguien la carga el 20 de abril. **`fecha_aprobacion` de
§D3 es el `ocurrido_at` de la transición a `Aprobada`**, nunca el `registrado_at`:
usar este último haría que la vigencia formal dependiera de cuándo alguien tuvo
tiempo de cargar el dato.

`[C]` El asiento es **append-only** por la misma razón que el avalúo emitido lo es
(ADR-0069 D3): si una fecha de transición pudiera corregirse, toda corrida pasada
dejaría de ser reproducible. Una carga errónea se corrige con una versión nueva,
no editando el historial.

*Vigente* no se almacena. Cambiaría solo por el paso del tiempo, lo que obligaría a
un proceso programado cuyo fallo sería silencioso, y destruiría la vigencia
histórica necesaria para reproducir corridas pasadas.

### D3 — Vigencia formal y aplicabilidad excepcional

#### Qué devuelve qué

`[C]` Regla transversal, y la más fácil de perder al implementar:

| Situación | Respuesta |
|---|---|
| Configuración válida que no satisface la condición | **`False`** |
| Versión o grafo normativo mal formado | **`ConfiguracionInvalida`** |
| Corrida incompatible o snapshot inválido | **`CorridaInvalida`** |

**Un predicado nunca responde `False` sobre una configuración inválida.** `False`
conflaciona *"esta versión no es aplicable"* con *"esta versión está mal formada"*,
y un GAM que recibe `False` corregiría sus fechas cuando lo que tiene es una
configuración inconsistente. Los validadores usan solo operaciones primitivas y
**nunca llaman a los predicados**, para evitar ciclos.

#### Cobertura de gestión

```
cubre_gestion(v, gestion) =
      gestion >= v.gestion_desde
  AND ( v.gestion_hasta IS NULL OR gestion <= v.gestion_hasta )
```

**`gestion_hasta = NULL` significa *sin límite superior conocido*,** no *no
vigente*. Los extremos de gestión son **inclusivos**.

#### Ventana normativa

`[C]` **Reformulación respecto de la versión 4.** Aquella definía la ventana por
*resoluciones* con cuantificador existencial. Esa formulación admitía uniones
discontinuas: dos alternativas con un hueco entre ellas harían que una versión
fuera vigente, dejara de serlo y volviera a serlo sin ninguna transición de estado.

La formulación adoptada:

```
Por GRUPO: union de las ventanas de sus alternativas.
           La union DEBE ser un unico intervalo continuo.
Entre GRUPOS formales: interseccion.
```

**Una versión tiene una ventana única y continua, o su configuración es
inválida.** La intersección de intervalos es siempre un intervalo, así que la
continuidad queda garantizada por construcción y no por una regla adicional.

`[C]` Esto **no afirma que la realidad no pueda tener huecos.** Si dos instrumentos
alternativos dejan un hueco, se modela como `versión A → intervalo sin versión
aplicable → versión B`. Lo que no se permite es representarlo como una sola versión
que revive.

Una ventana vacía —inicio posterior o igual al fin— también invalida la
configuración.

#### Precedencia entre los tres relojes

`[C]` Una versión puede dejar de producir efectos por tres causas independientes:
su propio cese, el cese de un instrumento requerido, o el relevo de una sucesora.
**Gobierna el que se detenga primero.**

#### Vigencia formal histórica

```
VigentePara(v, gestion, fecha_corte) =
      configuracion de v valida            (si no, ConfiguracionInvalida)
  AND cubre_gestion(v, gestion)
  AND v.estado IN ('Aprobada','Reemplazada','Cesada')
  AND v.fecha_aprobacion < fecha_corte
  AND ( v.fecha_efecto_cese IS NULL OR fecha_corte < v.fecha_efecto_cese )
  AND ( v.fecha_inicio_reemplazo IS NULL OR fecha_corte < v.fecha_inicio_reemplazo )
  AND la ventana normativa de v contiene fecha_corte
```

**Los intervalos son semiabiertos `[inicio, fin)`.** El extremo superior es
exclusivo o dos versiones se solapan el día del relevo.

**La comparación de aprobación es estricta**, y su fundamento es el mismo que el de
§D3 más abajo: sin hora jurídica verificable no puede afirmarse que la aprobación
ocurrió antes de la fecha de corte.

Se admiten los estados terminales porque **una versión hoy `Reemplazada` o `Cesada`
pudo haber sido vigente en el pasado**. Consultar solo el estado presente haría
irreproducible cualquier corrida anterior, rompiendo el eje que ADR-0069 D2
estableció.

#### Aplicabilidad excepcional

```
AplicableA(v, gestion, corrida) =
      VigentePara(v, gestion, corrida.fecha_corte)
  OR  (     cubre_gestion(v, gestion)
        AND v.estado IN ('Aprobada','Reemplazada','Cesada')
        AND v.fecha_aprobacion IS NOT NULL
        AND ( v.fecha_efecto_cese IS NULL
              OR corrida.fecha_corte < v.fecha_efecto_cese )
        AND ( v.fecha_inicio_reemplazo IS NULL
              OR corrida.fecha_corte < v.fecha_inicio_reemplazo )
        AND corrida.fecha_corte < fin de la ventana normativa
        AND existe un fundamento (§D6) que cubre esa gestion,
            cuya fecha_aplicacion_desde <= corrida.fecha_corte,
            y que autoriza expresamente TODOS los grupos formales que
            no cubren por si solos la fecha de corte
        AND v.fecha_aprobacion < fecha_local(corrida.iniciada_at) )
```

**La rama excepcional levanta únicamente el extremo INFERIOR de las ventanas
instrumentales, y solo para los grupos que el fundamento nombra.** Nunca levanta el
fin instrumental, el cese propio ni el relevo.

`[C]` **La rama excepcional NO exige `fecha_aprobacion < fecha_corte`.** Una norma
aprobada después de la fecha de corte es exactamente el caso que esta rama existe
para cubrir; exigirlo la dejaría muerta para la retroactividad.

Precisiones que la implementación no debe inventar:

- El instante de comparación es **`iniciada_at` de la corrida**, no `fecha_corte`
  ni `emitida_at`. La norma debe estar aprobada antes de que el cálculo empiece.
- Se usa el **`iniciada_at` original e inmutable**, no el de un reintento.
- `fecha_local()` convierte el `timestamptz` a la **fecha de Bolivia**, porque la
  aprobación se almacena como `date` y no se comparan tipos distintos.
- La comparación es **estricta**: una aprobación del mismo día no califica.
- Por lo anterior, **`AplicableA` recibe la corrida**, no solo `fecha_corte`.

`[C]` **Separar los dos predicados es una decisión de fondo.** Con un solo
predicado estirado para cubrir el caso excepcional, el sistema afirmaría que una
norma aprobada en 2025 *"estaba vigente"* en 2024, lo que es falso. **El alcance
puede ser anterior; la aprobación nunca.**

**Ningún predicado depende de `now()`.**

#### Tabla de casos

`[C]` Prueba de consistencia de la prosa anterior, verificada por el modelo
ejecutable. **Precondición común: toda condición no mencionada en la descripción
del caso se cumple, y no existe fundamento salvo que el caso lo diga
expresamente.** Sin esa precondición varios casos afirmativos serían
indeterminados.

| # | Situación | `VigentePara` | `AplicableA` |
|---|---|---|---|
| 1 | `Aprobada`, gestión cubierta, la ventana cubre `fecha_corte` | sí | sí |
| 2 | `Aprobada`, gestión **fuera** de `[desde, hasta]` | no | no |
| 3 | `fecha_corte` anterior al inicio, **sin** fundamento | no | no |
| 4 | igual que 3, **con** fundamento y aprobación anterior a `iniciada_at` | no | **sí** |
| 5 | igual que 4, aprobación **el mismo día** que `fecha_local(iniciada_at)` | no | no |
| 6 | igual que 4, pero un grupo requerido **no autorizado** por el fundamento | no | no |
| 7 | `Cesada` con `fecha_efecto_cese <= fecha_corte` | no | no |
| 8 | `Cesada` con `fecha_efecto_cese > fecha_corte`, instrumentos vigentes | **sí** | sí |
| 9 | `Reemplazada` con `fecha_corte < fecha_inicio_reemplazo` | **sí** | sí |
| 10 | `Reemplazada` con `fecha_corte >= fecha_inicio_reemplazo` | no | no |
| 11 | un instrumento del grupo cesó, **alternativa vigente en el mismo grupo** | **sí** | sí |
| 12 | el **único** instrumento del grupo cesó antes de `fecha_corte`, con fundamento | no | no |
| 13 | `PropuestaTecnica` | no | no |
| 14 | `Descartada` | no | no |
| 15 | aprobación **el mismo día** que `fecha_corte` | no | no |
| 16 | aprobación **el día anterior** a `fecha_corte` | **sí** | sí |
| 17 | aprobación **posterior** a `fecha_corte`, sin fundamento | no | no |
| 18 | **retroactividad**: aprobación posterior a `fecha_corte`, con fundamento | no | **sí** |
| 19 | igual que 18 **sin** fundamento | no | no |
| 20 | igual que 18 pero `fecha_corte` anterior a `fecha_aplicacion_desde` | no | no |

`[C]` La tabla **no es exhaustiva**: cubre las combinaciones que las auditorías
encontraron problemáticas. Un caso no listado puede seguir siendo ambiguo.

### D4 — Aprobación por conjunto, con rol, objetivo, grupo y gate

`parametros_version` es lo que pasa de `PropuestaTecnica` a `Aprobada`. Los
parámetros individuales conservan su origen, su evidencia y su marca, pero **no
tienen estados de aprobación independientes**.

#### Los cuatro roles

El vocabulario de roles es **cerrado**. Un mismo instrumento puede desempeñar
varios roles; **cada rol se registra por separado**.

| Rol | Objetivo que identifica | Gate al que pertenece |
|---|---|---|
| `APRUEBA_PARAMETRO` | `parametro_codigo` concreto | aprobación de la versión y `PreviewListo` |
| `REGLAMENTA_REGLA` | una `formula_version` concreta | uso de esa fórmula y `PreviewListo` |
| `HABILITA_EMISION` | gestión o intervalo de gestiones | **únicamente** `Emitida` |
| `REFERENCIA` | — | ninguno |

`[C]` **`REGLAMENTA_REGLA` identifica una `formula_version`, no "una regla o una
`formula_version`".** Dos clases de objetivo sin identificador común harían la
clasificación no reproducible.

#### Concurrencia y alternativa

Cada requisito lleva un **`grupo_requisito`**:

- **todos los grupos de un gate deben satisfacerse** — semántica `AND`;
- **dentro de un grupo basta uno** de sus instrumentos — semántica `OR`;
- instrumentos **conjuntamente obligatorios** van en **grupos distintos**;
- instrumentos **alternativos** van en el **mismo grupo**;
- un instrumento puede satisfacer varios grupos mediante vínculos separados;
- **el `grupo.id` es único dentro de una versión.**

`[C]` La unicidad del `grupo.id` no es cosmética: sin ella dos requisitos distintos
colapsan en cualquier estructura indexada por ese identificador, y la corrida deja
de conservar un instrumento por cada grupo real.

**El grupo pertenece al requisito, y el requisito declara su gate.** La regla es
*todos los grupos **de ese gate***, no todos los grupos.

`[C]` **Si dos instrumentos no son realmente intercambiables para el requisito, no
deben modelarse como alternativas del mismo grupo.**

#### Cobertura de un parámetro

**La cobertura es una relación con grupos, no una referencia a un instrumento
singular:**

> Un parámetro `[C]` está cubierto cuando **todos sus grupos requeridos** tienen al
> menos un instrumento aplicable.

Un parámetro puede requerir **varios instrumentos conjuntamente** —varios grupos— y
tener **alternativas** dentro de cada grupo.

`[C]` La versión 2 proponía una clave foránea compuesta hacia el par
`(version, instrumento)`. **Esa clave no puede existir:** un instrumento puede tener
varios roles sobre la misma versión, de modo que el par no es único. Y aunque lo
fuera, solo probaría pertenencia, no que exista un rol `APRUEBA_PARAMETRO` dirigido
a ese parámetro. La forma física es la obligación **E-1**.

#### Reglas del gate

1. Un conjunto incompleto **puede** calcular de forma diagnóstica durante
   `EnEjecucion`.
2. Un conjunto incompleto **no puede alcanzar `PreviewListo`**, y por lo tanto
   tampoco `Emitida`. Es el gate exacto de ADR-0069 D8.
3. Para aprobarse, todo parámetro marcado `[C]` debe estar cubierto. **Basta que
   exista el instrumento definitivo; no se exige que haya entrado en vigor**
   (§D12).
4. Una corrida referencia **exactamente un** `parametros_version_id`.

#### Habilitación de emisión: lectura estricta

`[C]` **Decisión, no consecuencia jurídica:**

> Sin al menos un grupo `HABILITA_EMISION` aplicable a la gestión, la corrida no
> puede alcanzar `Emitida`.

**La ausencia de filas significa *habilitación no acreditada*, no *habilitación
innecesaria*.** En una emisión tributaria conviene fallar cerrado. Si el GAM
determina que no se requiere un acto adicional, eso debe declararse expresamente en
otro criterio; nunca inferirse del silencio. Un mismo instrumento puede asumir
también este rol si su contenido realmente habilita la emisión.

`[D]` La Ley 482 exige Ley Municipal para aprobar zonificación, valuación zonal y
tablas de valores (arts. 13, 23 y 26.17), pero **no resuelve por sí sola la
semántica de la ausencia de este gate**.

### D5 — Modelo del instrumento normativo y catálogo de su naturaleza

`[D]` Marco declarado por el orquestador, no verificado en esta sesión: la Ley 482
clasifica el instrumento como **Ley Municipal**; su art. 26.17 exige que
zonificación, valuación zonal y tablas de valores sean propuestas por el Ejecutivo
y aprobadas por Ley Municipal; sanción por el Concejo, promulgación, y vigencia
desde la publicación oficial salvo fecha distinta.

`[D]` Para Uyuni la denominación institucional reportada es **"Ley Autonómica
Municipal"** —no "Ley Municipal Autonómica", no "Ordenanza Municipal"—. La fuente
no fue examinada.

**Decisión:** el modelo separa la **clase jurídica** de la **denominación
literal**, porque el sistema es nacional y la denominación puede variar entre
municipios sin que cambie la naturaleza del acto.

```
naturaleza_normativa          FK a catalogo del sistema
denominacion_literal          texto tal como el municipio la nombra
numero, gestion
titulo, objeto
fecha_sancion
autoridad_promulgadora, fecha_promulgacion
fecha_publicacion, medio_oficial_publicacion
fecha_entrada_vigencia
fecha_cese, acto_cese                        nulos si sigue en vigor
archivo_sha256
instrumento_reemplazado_id
vinculo N:M con parametros_version, con rol, objetivo y grupo (§D4)
```

**Sanción, promulgación, publicación, entrada en vigor y cese son hechos distintos
y se conservan por separado.** Colapsarlos haría imposible determinar qué regía en
un momento dado, que es lo que §D3 necesita.

**Los dos ceses son independientes y no se derivan uno del otro.** `fecha_cese` es
del **instrumento**; `fecha_efecto_cese` es de la **versión de parámetros** (§D2).
`[C]` Un instrumento puede cesar sin que la versión cese —si el grupo tiene
alternativa vigente— y una versión puede cesar por acto propio con todos sus
instrumentos en vigor. **Ambos entran en la precedencia de §D3, y gobierna el que
se detenga primero.**

**Invariantes del reemplazo de instrumentos**, análogas a las de §D2: mismo
municipio, sucesor distinto, sin ciclos, y `fecha_entrada_vigencia` del sucesor no
anterior a la del reemplazado.

`archivo_sha256` es obligatorio: el instrumento se registra por identidad de
contenido, no por nombre de archivo.

**`naturaleza_normativa` es un catálogo del sistema**, no un `CHECK` cerrado. `[D]`
Su contenido: Ley Municipal, Resolución del órgano legislativo, Decreto Municipal,
Decreto Edil y Resolución Administrativa Municipal. Códigos estables asignados por
la especificación, ampliable sin migración, altas solo por operación privilegiada y
auditada, **sin borrado ni reutilización de códigos**.

`[D]` El argumento contra sembrar solo dos tipos es que Uyuni usaría una Resolución
Administrativa Municipal dentro de su cadena tributaria — afirmación documental
cuya fuente tampoco fue examinada.

### D6 — Aplicación anterior: fundamento estructurado y obligatorio

`[D]` Fundamento declarado por el orquestador y no verificado aquí: la CPE dispone
que la ley rige hacia el futuro (art. 123) y el Código Tributario establece que las
normas tributarias no son retroactivas salvo casos tasados, entre ellos cuando
benefician al sujeto pasivo (arts. 3 y 150).

**Decisión:**

1. **No existe una bandera general de retroactividad.**
2. La aplicación comienza en la **fecha de entrada en vigor** del instrumento.
3. Una aplicación anterior solo es posible si existe un **fundamento registrado
   como entidad propia**, vinculado al instrumento y **al alcance concreto que
   autoriza**, con al menos: tipo tomado de catálogo; disposición y artículo
   invocados; fundamentación textual obligatoria; `fecha_aplicacion_desde` y
   alcance temporal; autoridad que asumió la interpretación, identificada; fecha y
   usuario de registro.
4. **Los avalúos ya emitidos nunca se modifican.**
5. Cualquier efecto posteriormente autorizado produce una **nueva corrida y una
   nueva emisión vinculada al antecedente**.

**El fundamento debe identificar qué grupos autoriza a usar antes de su inicio.**
`[C]` Una referencia genérica a la gestión no alcanza en una resolución
multiinstrumento, y **todo grupo que el fundamento nombre debe existir en esa
versión y ser de rol `APRUEBA_PARAMETRO`**: una referencia colgante o de rol
improcedente invalida la configuración.

`[C]` **El tipo no autoriza automáticamente.** Un tipo genérico debe exigir cita
jurídica y motivación textual. Y **el motor no concluye por sí solo que un
resultado menor beneficia al sujeto pasivo**: esa calificación es jurídica, la
asume una autoridad y queda registrada con su nombre.

`[C]` **Por qué entidad separada y no un campo de texto.** Un campo libre sería
flexible pero no auditable: no permitiría saber quién asumió la interpretación ni
qué alcance exacto cubre.

`[C]` La inmutabilidad de ADR-0069 D3 conserva trazabilidad pero **no vuelve
lícita** una aplicación retroactiva. Son cosas independientes.

`[D]` **La licitud concreta debe confirmarla la asesoría jurídica del GAM.** El
sistema provee la estructura para registrarla y auditarla; no la valida.

### D7 — Divergencia entre el valor del GAM y el valor derivado

El sistema **aplica el valor del GAM sin excepción** y registra la divergencia a
**nivel de corrida**, con: valor aplicado, valor derivado, variación, instrumento
que fija el valor aplicado y **la evidencia del derivado, copiada al momento de la
corrida** — no una referencia que pueda cambiar después.

```
variacion_relativa = (aplicado - derivado) / abs(derivado)
```

- Solo para parámetros numéricos y solo cuando `derivado <> 0`.
- `NULL` cuando `derivado = 0`, y siempre para texto y booleanos: una diferencia
  categórica no tiene magnitud.
- **Se registra igual aunque la variación sea nula.**
- **Los valores se comparan tras convertirlos al tipo declarado del parámetro y
  normalizar decimales.** La comparación nunca se hace sobre la representación
  textual: `1`, `1.0` y `1.00` no son divergencias.

`[C]` Se descartaron las dos alternativas: obedecer en silencio pierde la única
evidencia que protege al GAM si el avalúo es impugnado; exigir confirmación sobre un
umbral pone al sistema a condicionar un acto de autoridad municipal y agrega un
umbral que nadie puede fundamentar.

`[C]` **Limitación intrínseca:** solo hay divergencia registrable donde el sistema
**tiene** un valor derivado. Para los no derivables del corolario 2 de §D1 hay
adopción, no divergencia. El registro cubre los parámetros metodológicos, no los
que más pesan sobre la base imponible.

### D8 — Corrección terminológica de los ADR anteriores

Donde ADR-0068, ADR-0069 y ADR-0070 dicen **"ordenanza"**, debe entenderse **Ley
Municipal**, denominada **"Ley Autonómica Municipal"** en Uyuni. **Los ADR
aceptados no se reescriben.**

`[D]` "Ordenanza Municipal" sería la denominación anterior al marco autonómico
vigente. `[C]` Es criterio la lectura de que en los tres ADR se quiso decir
*instrumento normativo municipal competente*, y que el error es de denominación y
no de sustancia.

### D9 — El vocabulario de códigos municipales requiere mapeo versionado

`[C]` **Hipótesis operativa, no hecho nacional observado:** cada GAM traerá su
propio vocabulario de códigos y ninguno traerá diccionario. Se observó en un
municipio.

`[V]` Lo medido en Uyuni:

- `capa_parcelas.uso_terreno` tiene **quince códigos distintos no vacíos** —`VIV`,
  `TRR`, `SIN`, `COM`, `SER`, `TRU`, `EDU`, `OFI`, `DEP`, `SAL`, `REC`, `IND`,
  `REL`, `CMC`, `CUL`— y **246 valores vacíos**.
- `dominio.catalogo_uso_suelo` contiene **quince categorías** normalizadas.
- `dominio.predios.uso_suelo_id` está poblado en **0 de 11.985** filas, y hay **0
  predios** enlazados al catálogo.
- `CatalogoPresentacionMunicipal.cs`, de **1.692 bytes**, enumera **quince** claves
  con **cero** etiquetas no nulas y la leyenda *"pendiente de diccionario oficial"*.
- ADR-0056 mide los mismos quince códigos, declara que no existe diccionario formal
  y exige preservarlos como opacos.
- ADR-0070 D7 registra **64 predios con `LAD`**, material ausente de la tabla `IPV`
  de RM 024/2024.

**Decisión:** el sistema incorpora un **mecanismo de mapeo de vocabulario
versionado por municipio**, que traduce los códigos de origen a las categorías que
la norma nacional define. El mapeo es dato del GAM y sigue el circuito de
aprobación de §D4.

`[C]` **El mecanismo no está definido** —dominios admitidos, categorías destino,
vigencia, reemplazo, códigos no mapeados— y **por eso M016 no lo crea**. Es
Pendiente 4, y su ausencia no bloquea el resto del esquema.

### D10 — Las fórmulas son código; su selección es dato trazable

`[V]` El efecto medido de la elección de capítulo es del orden del 10% sobre la
base imponible: `PLA` cubre **11.731 de 11.985** predios de Uyuni y vale 1,10 bajo
Cap. IV contra 1,00 bajo Cap. VI. `[V]` La medición es de ADR-0066 D2. `[C]` La
elección entre capítulos es criterio.

**Decisión:**

1. **El algoritmo y las tablas de cada capítulo son definición versionada y
   controlada por el sistema.** Nunca una expresión libre cargada como parámetro.
2. **La selección es un campo de la corrida**, `formula_version`, conforme a
   ADR-0069 D2, que ya lo declara como uno de sus cuatro ejes.
3. El Capítulo IV es la versión predeterminada, conforme a ADR-0066 D2.
4. El Capítulo VI solo puede seleccionarse si está **implementado, probado y con
   respaldo jurídico municipal validado** mediante un instrumento con rol
   `REGLAMENTA_REGLA`.
5. **Cada `parametros_version` declara con qué `formula_version` es compatible**, y
   una corrida cuya `formula_version` no esté entre las compatibles **no es una
   corrida válida**. La cardinalidad y su representación son la obligación **E-3**.

`[C]` El punto 5 impide combinar parámetros calibrados para el Capítulo IV con una
corrida de Capítulo VI, que **omite `Ff`** y usa otra tabla de `Fi`.

### D11 — Integridad multi-municipio y validación en dos niveles

`[V]` La base tiene hoy **dos municipios con dataset activo**, `051201` y `022001`.
`[C]` El riesgo de combinaciones cruzadas no es hipotético.

`[V]` Medición del 4 de agosto:

| Tabla | Claves existentes |
|---|---|
| `dataset_versiones` | PK `(id)` · UNIQUE total `(municipio_codigo, numero_version)` · UNIQUE **parcial** `(municipio_codigo)` donde `estado='Activa'` |
| `predios` | PK `(id)` · UNIQUE total `(codigo_catastral)` · UNIQUE **parcial** `(municipio_codigo, cod_uv, cod_man, cod_pred)` donde `NOT is_deleted` |
| `capa_vias` | PK `(id)`, sin más |

**Ninguna tiene una clave única sobre `(id, segunda_columna)`**, que es la forma que
una clave foránea compuesta necesita como destino. PostgreSQL no acepta un índice
parcial como destino, lo que descarta los dos parciales existentes.

`[C]` **Un trigger no cierra el problema.** Verifica al insertar o actualizar la
fila de `valuacion`, pero si después alguien cambia
`dominio.predios.municipio_codigo`, nada se dispara y la relación queda rota en
silencio.

**Decisión: M016 agrega tres restricciones únicas a `dominio`**, exclusivamente
para servir de destino a claves foráneas compuestas:

```
dominio.dataset_versiones   UNIQUE (id, municipio_codigo)
dominio.predios             UNIQUE (id, municipio_codigo)
dominio.capa_vias           UNIQUE (id, dataset_version_id)
```

Restricciones `UNIQUE` reales, **no índices parciales**.

`[V]` `id` es clave primaria en las tres tablas y las seis columnas involucradas son
`NOT NULL`. `[C]` Por lo tanto `(id, cualquier_cosa)` es único por construcción:
**no rechazan ningún dato presente ni futuro, no cambian ninguna semántica y no
pueden fallar al aplicarse.** Su único propósito es existir como destino.

Las claves foráneas desde `valuacion` **no llevan `CASCADE`** e impiden actualizar
o borrar del lado referenciado si eso rompería la relación. La elección entre
`RESTRICT` y `NO ACTION` y la diferibilidad son la obligación **E-4**.

`[C]` Esta decisión **autoriza a M016 a modificar `dominio` en ese punto acotado y
solo en ese**.

`[V]` Volúmenes: 6, 11.985 y 2.986 filas. `[C]` **El costo no fue medido**: se
midieron cantidades de filas, no latencia.

#### Validación en dos niveles

`[C]` Una versión aislada no contiene a las demás, de modo que la integridad se
verifica en dos niveles distintos:

| Nivel | Alcance |
|---|---|
| **Configuración** | invariantes locales de una versión: vocabulario, unicidad de `grupo.id`, referencias de instrumento, coherencia de estado terminal, referencias de fundamento, continuidad y ventana |
| **Modelo** | integridad del registro: la clave es el `id`, el `id` es único, la sucesora existe, cada arista de sucesión es válida, no hay ciclos ni convergencias |

**Reglas del validador global:**

- **nunca propaga una excepción**: devuelve errores estructurados;
- **no compone una arista hasta que ambos extremos pasan su validación local**,
  porque componer sobre datos malformados produce errores derivados sin significado;
- **reporta cada ciclo una vez**, no una por rotación.

**Ningún flujo operativo procede dentro de un registro globalmente inválido**, y
**el registro debe contener exactamente la versión evaluada**: un registro ajeno,
aunque sea internamente válido, no acredita nada sobre ella.

### D12 — Ciclo de vida de la corrida y su snapshot

#### Qué se congela en cada estado

| Estado | Snapshot exigido |
|---|---|
| `EnEjecucion` | **ninguno**: la resolución se congela AL ejecutar |
| `PreviewListo` | los grupos formales y los `REGLAMENTA_REGLA` de su fórmula |
| `Emitida` | además, los `HABILITA_EMISION` que cubren su gestión |

`[C]` Exigir el snapshot para decidir si se puede ejecutar sería circular: la
resolución se congela al ejecutar, y el gate pregunta si se puede ejecutar. Por eso
`EnEjecucion` no lo exige y los estados posteriores sí.

#### Qué puede congelarse

**Cualquier instrumento del grupo que sea aplicable para esa corrida** —incluido el
caso del fundamento— **puede congelarse; no tiene que coincidir con la selección
automática del sistema.** La selección automática es una **propuesta por defecto**,
no una autoridad. El snapshot representa la elección efectivamente utilizada.

`[C]` Si dos instrumentos no son realmente intercambiables, no deben ser
alternativas del mismo grupo (§D4).

#### Monotonía e identidad

**El snapshot solo CRECE entre gates:**

- agregar grupos nuevos: permitido;
- **sustituir una elección ya congelada: prohibido**;
- **eliminar un grupo ya congelado: prohibido**;
- un mismo grupo no puede aparecer dos veces en ningún snapshot.

`[C]` Si una elección congelada pudiera cambiar, el registro dejaría de ser el de lo
que efectivamente se usó, que es toda su razón de existir.

**Y la corrida es la misma en todos sus campos, no solo en su identificador.** Estos
siete son **inmutables** a lo largo del ciclo de vida:

```
id · municipio · parametros_version_id · formula_version
gestion · fecha_corte · iniciada_at
```

`[C]` Comparar solo el `id` permitiría **preparar una corrida con una fórmula,
gestión o fecha de corte y emitirla con otra distinta conservando el
identificador**. Lo único que puede variar es el snapshot.

#### Aprobación y corrida son independientes

`[C]` **El gate de aprobación no recibe corrida.** La aprobación precede
conceptualmente a cualquier corrida y sus campos serían irrelevantes. Es además
**gate de transición**: solo se aprueba lo que está en `PropuestaTecnica`.

Para aprobar basta que exista el **instrumento definitivo** que aprueba cada
parámetro: **no se exige que haya entrado en vigor**. El instrumento no puede ser un
borrador — debe estar registrado como acto finalizado, identificado por contenido y
con el hecho aprobatorio ocurrido. Su fecha de entrada en vigor se usa después en
`VigentePara`, no en la transición a `Aprobada`.

**La corrida conserva cuál instrumento resolvió cada grupo requerido.** No basta con
poder recalcular después sobre el catálogo vigente: el catálogo cambia y la
reproducibilidad exige el registro del momento. La forma física es la obligación
**E-2**.

---

## Consecuencias

- **M016 crea estructura vacía de datos municipales**, más dos catálogos del
  sistema, más las tres restricciones únicas de §D11, más las cuatro obligaciones
  E-1 a E-4. *Vigente* no es columna. **M016 no crea el mecanismo de mapeo de §D9.**
- **`IPES` sigue bloqueado** por falta de diccionario semántico (§D9).
- **La campaña de encuestas de ADR-0067 debe incorporar una pregunta de
  disponibilidad de servicio en la vía**, separada de la conexión del predio. Ni
  ADR-0067 E1 ni ADR-0045 D9 la contemplan.
- **La incorporación de un municipio nuevo tiene un procedimiento declarado:**
  cargar cartografía, derivar los parámetros derivables sobre sus datos, dejar
  vacíos los monetarios, mapear su vocabulario, y esperar instrumento.

### Se declara una tercera dependencia externa del GAM

ADR-0069 declaró que `Mv` no agregaba una tercera dependencia, y **esa afirmación
era correcta para `Mv`**. Este ADR descubre una distinta, procedente de `IPES`.
ADR-0069 no queda invalidado.

**Criterio de conteo:** una dependencia es externa cuando solo una autoridad externa
puede resolverla, el equipo técnico no puede inferirla legítimamente, y su ausencia
bloquea el cálculo. **Se cuenta por esos criterios, no por el esfuerzo.**

| | Dependencia | Naturaleza | Destraba |
|---|---|---|---|
| **E1** | Campaña de encuestas | operativa | `VPz` / `Vz` |
| **E2** | Valores constructivos aprobados | normativa | `Tip` |
| **E3** | Diccionario `uso_terreno` → categorías `IPES` | documental liviana | `IPES` y `VSz` |

---

## Limitaciones declaradas

### L1 — Las citas jurídicas no fueron verificadas

`[D]` Ley 482 arts. 13, 23 y 26.17; CPE art. 123; Código Tributario arts. 3 y 150;
la denominación institucional de Uyuni; el uso de una Resolución Administrativa
Municipal en su cadena tributaria; la existencia de la Ley 090/2024 y sus normas
derivadas; y la afirmación sobre "Ordenanza Municipal". Todas provienen del
orquestador y **ninguna fue leída en su fuente**. El equipo técnico no tiene
competencia para interpretarlas. Deben confirmarse antes de que este modelo
sostenga una emisión tributaria.

### L2 — El registro de divergencia no cubre los parámetros que más pesan

`Vz` y `Tip` no tienen valor derivado contra el cual comparar.

### L3 — El modelo del instrumento se diseñó sin haber visto ninguno

`[V]` No se localizó ningún instrumento normativo municipal en disco. **El primero
que se cargue va a poner a prueba este modelo**, y es esperable que falte algún
campo.

### L4 — El mecanismo de mapeo de §D9 no está definido

M016 no lo crea. Ver Pendiente 4.

### L5 — Las quince posiciones de catálogo salen de un municipio

`[V]` Caranavi no tiene ninguna calibrada y sus **6.573** predios siguen bloqueados
por los **11** con partes secundarias de área ≈ 0 (ADR-0068 L5).

### L6 — La tabla de casos no es exhaustiva

Cubre las combinaciones que las auditorías encontraron problemáticas. Un caso no
listado puede seguir siendo ambiguo.

### L7 — Alcance y límites del modelo ejecutable

`[C]` El modelo **falsa coherencia interna; no acredita adecuación jurídica**.

**Lo que el modelo NO cubre**, y que por lo tanto se implementará sin respaldo
ejecutable: la máquina de transiciones y su historial (§D2), la validación del
objetivo de `APRUEBA_PARAMETRO` como código concreto de parámetro (§D4), la
semántica propia del rol `REFERENCIA` (§D4) y toda persistencia. `[C]` Esa columna
merece más cuidado en M016, no menos.

`[C]` Detalle de interfaz, sin efecto semántico: el despachador genérico de gates
del modelo recibe una corrida también para el gate de aprobación, aunque la
ignora. La función específica de aprobación **no la recibe**, conforme a §D12.

**Cobertura del producto finito.** `[C]` Las cifras tienen **dos procedencias
distintas** y mezclarlas sería presentar como reproducible lo que no lo es:

| Medición | Valor | Procedencia |
|---|---|---|
| Configuraciones evaluadas | 144 | `[V]` ejecución del modelo `281a94e1…` |
| Semánticamente distintas | 144 | `[V]` ejecución del modelo |
| Testigos donde el fundamento cambia el resultado | **3** | `[V]` ejecución del modelo |
| Testigos de aprobación sin vigencia | **12** | `[V]` ejecución del modelo |
| Comprobaciones · mutantes · control | 162 · 38 · 1 | `[V]` ejecución del modelo |
| Configuraciones del producto antiguo | 1.080 | `[D]` auditoría de la tercera pasada |
| De ellas, rechazadas por la validación | 912 | `[D]` auditoría de la cuarta pasada |
| De ellas, duplicados semánticos | 24 | `[D]` auditoría de la cuarta pasada |
| Testigos antiguos del fundamento | 12 | `[D]` auditoría de la tercera pasada |
| Testigos antiguos de aprobación sin vigencia | 216 | `[D]` auditoría de la tercera pasada |

`[C]` **Las cinco últimas no se reproducen ejecutando el modelo actual**: describen
un producto que ya no existe. Provienen de dictámenes de auditoría y se marcan
`[D]` en consecuencia.

`[C]` La reducción es legítima: las 936 que se fueron eran 912 configuraciones
inválidas y 24 duplicados. Pero **baja los testigos de dos propiedades
existenciales**, y eso se compensó con contrapruebas dirigidas por clase de
equivalencia, no con volumen.

`[V]` La **sexta pasada de auditoría ejecutó su arnés en memoria** por agotamiento
de cuota del mecanismo de edición, de modo que **no existe hash de sus
artefactos**, a diferencia de las cinco anteriores.

`[C]` Los defectos que las auditorías encontraron se concentraron en la **capa de
validación estructural**, no en los predicados temporales: estos coincidieron sin
discrepancias contra **tres oráculos escritos independientemente** desde el texto
de este ADR.

---

## Criterios de diseño, no consecuencias de la medición

La frontera de §D1 y su corrección a ADR-0066 D6 · derivar *Vigente* · los cinco
estados y sus transiciones · tratar las causas de cese como fundamento · rechazar
la convergencia de sucesoras · modelar la suspensión levantada como versión nueva ·
separar `VigentePara` de `AplicableA` · el instante de comparación · el significado
de `gestion_hasta = NULL` · la ventana por intersección de uniones continuas · la
precedencia del primero en detenerse · la definición de `fecha_inicio_reemplazo` ·
los cuatro roles, sus objetivos y la semántica de grupos por gate · la lectura
estricta de `HABILITA_EMISION` · el fundamento como entidad · la fórmula de
`variacion_relativa` · mantener el capítulo como definición del sistema y su
selección como dato · agregar tres restricciones únicas a `dominio` · la
inmutabilidad de los siete campos de la corrida · la distinción entre `False` y
excepción.

Cuatro merecen fundamento explícito:

**Derivar *Vigente*.** Almacenarlo es más simple de consultar, pero introduce un
proceso programado cuyo fallo es silencioso y destruye la vigencia histórica
necesaria para reproducir corridas pasadas.

**Registrar la divergencia sin bloquear.** El sistema es una herramienta del GAM y
de la población, no un contralor de la autoridad municipal. Pero una herramienta que
no deja constancia de lo que midió no protege a nadie cuando un avalúo se impugna.

**Modificar `dominio` para agregar tres claves únicas.** Rompe el principio de que
M016 no toca `dominio`. Sin ellas la integridad multi-municipio no es expresable
como restricción de base, y la alternativa —triggers— solo verifica en el momento de
escribir. Las claves son inocuas por construcción, pero el principio se rompe y eso
queda declarado, no disimulado.

**La lectura estricta de `HABILITA_EMISION`.** Fallar cerrado es una elección: un
municipio podría sostener que sin acto habilitante expreso la emisión procede igual.
La razón para elegir lo contrario es que se trata de una emisión tributaria, y que
el silencio no debe convertirse en autorización por omisión.

---

## Pendiente

`[C]` Cada pendiente declara si **bloquea M016** o no. Un pendiente cuyo objeto el
ADR excluyó expresamente del alcance de la migración no puede bloquearla; decirlo
aquí evita que se vuelva a discutir.

1. **Verificar las citas jurídicas** de §D4, §D5, §D6 y §D8 (L1).
   **No bloquea M016**; bloquea la emisión tributaria.
2. **Definir el catálogo de tipos de fundamento de aplicación anterior** (§D6) con
   la asesoría jurídica del GAM. **No bloquea M016**: la migración crea la tabla
   vacía; su contenido es jurídico y municipal.
3. **Auditar la Ley 090/2024 de Uyuni, su Decreto Municipal 23/2025 y la Resolución
   Administrativa Municipal 48/2025** cuando se obtengan, con identidad por hash.
   Permitirán poner a prueba el modelo de §D5. **No bloquea M016.**
4. **Completar el mecanismo de mapeo de vocabulario de §D9. No bloquea M016**,
   porque §D9 y L4 excluyen expresamente ese mecanismo del alcance de la
   migración. Bloquea `IPES` y, con él, `VSz`.
5. **Fijar `minimo_observaciones_zonal`** e `ipv_material_lad`, las dos posiciones
   de catálogo sin valor. **No bloquea M016**: el catálogo nace vacío.
6. **Reconciliar el motor contra la emisión de IMPBI 2024 de Uyuni**, si el padrón
   emitido está disponible. `[C]` Sería la validación externa más fuerte al
   alcance. **No bloquea M016.**
7. **Ejecutar la consulta de `IPES` por zona** para determinar si el sistema puede
   derivar un valor por defecto con la cartografía disponible. **No bloquea M016.**
8. **Implementar y probar el Capítulo VI** si algún municipio lo requiere (§D10
   punto 4). Hoy no existe. **No bloquea M016**: el Capítulo IV es el
   predeterminado y §D10 punto 4 condiciona la selección del VI a que esté
   implementado.

9. **Asignar los códigos estables de los dos catálogos del sistema** —
   `naturaleza_normativa` y motivos de QC. **Bloquea M016**, porque la migración
   los siembra. Es obligación de la especificación, no decisión de este ADR.
