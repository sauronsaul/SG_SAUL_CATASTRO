# ADR-0071 — Separación entre norma nacional y decisión municipal: aprobación, vigencia y divergencia

- **Estado:** Propuesta (borrador para revisión y aprobación de Saul).
- **Fecha:** 2026-08-04 · Fase 4.B.
- **Completa** D2 y D6 de ADR-0069, que fijaron `parametros_version` como eje de
  la corrida y previeron vigencia por `(municipio, gestion_desde)` sin definir
  cómo se aprueba ni qué significa estar vigente.
- **Corrige la terminología** de ADR-0068, ADR-0069 y ADR-0070 (§D8).
- **Precede a M016.** Sin este ADR el DDL introduciría la distinción entre
  aprobación y vigencia de forma implícita.
- **Naturaleza:** este ADR es casi enteramente **decisión de diseño**. Salvo
  donde se indique, su contenido no se deriva de ninguna medición y está sujeto
  a criterio. Se marca `[C]` por defecto.

---

## Contexto

Los ADR anteriores calibraron quince parámetros sobre los datos de Uyuni y los
declararon *propuesta técnica pendiente de ordenanza*. Esa formulación arrastra
un supuesto que no resiste el alcance real del producto: **el sistema no se
construye para Uyuni.** Se construye para los Gobiernos Autónomos Municipales de
Bolivia, y Uyuni y Caranavi son los dos primeros casos.

Bajo ese alcance, la pregunta correcta no es *qué valores adopta Uyuni* sino
**qué le corresponde decidir al sistema y qué le corresponde decidir a cada GAM**.
Este ADR fija esa frontera y el circuito por el que una decisión municipal entra
al sistema, se aprueba, entra en vigor y eventualmente es reemplazada.

`[V]` Comprobación previa: se buscó normativa municipal en las tres raíces del
entorno de trabajo con once patrones de nombre. **No existe en disco ningún
instrumento normativo municipal**; `docs/normativa/` contiene únicamente un
`.gitkeep` de 0 bytes. `[D]` Se ha reportado la existencia de una Ley Autonómica
Municipal N.º 090/2024 de Zonificación y Valuación Zonal de Uyuni, su Decreto
Municipal 23/2025 reglamentario y una tabla CVZ aplicada al IMPBI 2024. Ninguno
fue examinado. **Este ADR no depende de ellos**: describe el mecanismo por el
cual cualquier instrumento de cualquier municipio entra al sistema.

---

## Decisiones

### D1 — Frontera entre norma nacional y decisión municipal

**El sistema implementa la norma nacional. El GAM fija los valores.**

| Corresponde al sistema (código) | Corresponde al GAM (dato versionado) |
|---|---|
| La cadena `VPz → VSz → Mv → Vt` | Los valores zonales |
| Las tablas de coeficientes de RM 024/2024 | Los umbrales que la norma delega |
| El reparto tabulado de `Fs` | Los valores por m² de construcción |
| Las reglas de agregación zonal | Las asimilaciones de vocabulario |
| El cálculo de métricas geométricas | Toda cifra monetaria |

Todo lo que la norma nacional determina es código y **no es parametrizable por
municipio**. Todo lo que la norma delega es dato y **no puede estar compilado**.

**Corolario 1 — Los parámetros `[C]` de ADR-0068 y ADR-0070 no son propuestas
para Uyuni.** Son **valores por defecto que el sistema deriva de los datos del
propio municipio** cuando el GAM no ha fijado los suyos. Un GAM sin instrumento
normativo necesita que el sistema le muestre algo defendible y derivado de su
propia cartografía; un GAM con instrumento necesita cargar sus valores y que el
sistema los use. Ambos casos deben funcionar, y el segundo debe poder
sobrescribir al primero sin tocar código.

**Corolario 2 — No todo parámetro admite valor por defecto.** La línea es si el
parámetro exige un juicio monetario o información de mercado externa:

- **Derivables**: umbrales de forma, tolerancia de normalización, umbral de
  presencia zonal, umbral de asignación a vía, reglas de agregación. El sistema
  calcula un valor por defecto sobre los datos del municipio y lo declara como
  tal.
- **No derivables**: valor zonal `Vz`, valores por m² de construcción `Tip`,
  cualquier cifra monetaria. **Nacen vacíos y ningún valor por defecto es
  admisible.** Ratifica ADR-0067, que deja de ser una limitación de Uyuni y pasa
  a ser el comportamiento correcto del producto: **ningún GAM recibe valores
  monetarios inventados por el sistema.**

**Corolario 3 — Las limitaciones declaradas se releen como generales, no
locales.** El desvío de fuente de ADR-0070 D2 no es que a Uyuni le falte un dato:
es que **la norma nacional exige un atributo que el catastro urbano boliviano
típico no registra**, porque las capas de vía no llevan atributos de servicio.
Cualquier GAM con cartografía de ese tipo tendrá el mismo problema, y el sistema
debe declarar la sustitución y su magnitud en cada municipio en lugar de
resolverla en silencio.

### D2 — Tres estados almacenados; *Vigente* no es uno de ellos

Se almacenan **`PropuestaTecnica`**, **`Aprobada`** y **`Reemplazada`**.

Los tres cambian por un acto identificable —alguien propone, una autoridad
aprueba, un instrumento posterior reemplaza— y por eso se registran las **fechas
de cada transición**, o un historial de eventos equivalente.

*Vigente* no se almacena. Cambiaría solo por el paso del tiempo, lo que obligaría
a un proceso programado que voltee filas, y un proceso así puede fallar en
silencio dejando el sistema emitiendo con parámetros que ya no correspondían.

### D3 — `VigentePara(gestion, fecha_corte)` es un predicado reproducible

```
VigentePara(version, gestion, fecha_corte) =
      la version cubre esa gestion
  AND fue aprobada antes de fecha_corte
  AND su instrumento ya estaba en vigor a fecha_corte
  AND su reemplazo aun no habia entrado en vigor a fecha_corte
```

**El predicado nunca depende de `now()`.** Recibe `gestion` y `fecha_corte`
explícitos desde la corrida.

Fundamento: una versión hoy `Reemplazada` **pudo haber sido vigente
históricamente**. Consultar solo el estado actual perdería ese dato y haría
irreproducible cualquier corrida pasada, rompiendo el eje de reproducibilidad
que ADR-0069 D2 estableció.

### D4 — La unidad de aprobación es el conjunto, no el parámetro

`parametros_version` es lo que pasa de `PropuestaTecnica` a `Aprobada`. Los
parámetros individuales conservan su origen, su evidencia y su marca `[V]`/`[C]`,
pero **no tienen estados de aprobación independientes**.

Reglas:

1. Un conjunto incompleto **puede** calcular previews.
2. Un conjunto incompleto **no puede** sostener una corrida `Emitida`.
3. Para aprobarse, todos sus parámetros marcados `[C]` deben estar cubiertos por
   **uno o más** instrumentos jurídicos identificados.
4. Una corrida referencia **exactamente un** `parametros_version_id`. Nunca
   mezcla filas de versiones distintas.

La cardinalidad múltiple del punto 3 es deliberada: los parámetros metodológicos
y las tablas de valores pueden estar en instrumentos de rango distinto —una ley y
su decreto reglamentario— y el modelo debe admitirlo.

Enlaza con D8 de ADR-0069, que ya exige validar el catálogo antes de emitir. Este
ADR precisa qué se valida: cobertura por instrumento, no solo presencia de valor.

### D5 — Modelo del instrumento normativo

`[D]` Marco declarado por el orquestador, no verificado en esta sesión: la Ley
482 clasifica el instrumento como **Ley Municipal**, y su art. 26.17 exige que
zonificación, valuación zonal y tablas de valores sean propuestas por el
Ejecutivo y aprobadas por Ley Municipal. Sanción por el Concejo, promulgación, y
vigencia desde la publicación oficial salvo fecha distinta (arts. 13 y 23).

`[D]` Para Uyuni la denominación institucional comprobada es **"Ley Autonómica
Municipal"** —no "Ley Municipal Autonómica", no "Ordenanza Municipal"—.

**Decisión:** el modelo separa la **clase jurídica** de la **denominación
literal**, precisamente porque el sistema es nacional y la denominación puede
variar entre municipios sin que cambie la naturaleza del acto.

Campos mínimos:

```
naturaleza_normativa          LEY_MUNICIPAL | DECRETO_MUNICIPAL | ...
denominacion_literal          texto tal como el municipio la nombra
numero, gestion
titulo, objeto
fecha_sancion
autoridad_promulgadora, fecha_promulgacion
fecha_publicacion, medio_oficial_publicacion
fecha_entrada_vigencia
archivo_sha256
instrumento_reemplazado_id
vinculo N:M con parametros_version
```

**Sanción, promulgación, publicación y entrada en vigor son cuatro hechos
distintos y se conservan por separado.** Colapsarlos en una sola fecha haría
imposible determinar qué regía en un momento dado, que es exactamente lo que D3
necesita.

`archivo_sha256` es obligatorio: el instrumento se registra por identidad de
contenido, no por nombre de archivo.

### D6 — No hay retroactividad como regla general

`[D]` Fundamento declarado por el orquestador y no verificado aquí: la CPE
dispone que la ley rige hacia el futuro (art. 123) y el Código Tributario
establece que las normas tributarias no son retroactivas salvo casos tasados,
entre ellos cuando benefician al sujeto pasivo (arts. 3 y 150).

**Decisión:**

1. **No existe `permite_retroactividad = true` como bandera general.**
2. La aplicación comienza en la **fecha de entrada en vigor** del instrumento.
3. Solo se admite aplicación anterior cuando exista fundamento jurídico expreso
   registrado en el propio instrumento.
4. **Los avalúos ya emitidos nunca se modifican.**
5. Cualquier efecto posteriormente autorizado produce una **nueva corrida y una
   nueva emisión vinculada al antecedente**.

`[C]` La inmutabilidad de ADR-0069 D3 conserva trazabilidad pero **no vuelve
lícita** una aplicación retroactiva. Son cosas independientes y el modelo no debe
confundirlas: que un avalúo sea inmutable no autoriza a emitir otro con efecto
hacia atrás.

Nota de método: la pregunta *"¿es retroactivo aprobar en marzo para una gestión
iniciada en enero?"* **no puede responderse sin saber cuándo se perfecciona el
hecho imponible.** Esta decisión evita tener que responderla para poder construir
el modelo.

### D7 — Divergencia entre el valor del GAM y el valor derivado: se obedece y se registra

Cuando un GAM carga por instrumento un valor que difiere del que el sistema
derivó de los datos del propio municipio, **el sistema aplica el valor del GAM
sin excepción**, y registra la divergencia.

Se descartaron las dos alternativas:

- **Obedecer en silencio** — el sistema pierde la única evidencia que protege al
  GAM si el avalúo es impugnado después.
- **Exigir confirmación explícita sobre un umbral** — pone al sistema a
  condicionar un acto de autoridad municipal, y agrega un umbral más que nadie
  puede fundamentar.

**Decisión:** se registra a **nivel de corrida**, no de predio, porque la
divergencia es un hecho sobre un parámetro y no sobre un inmueble. La corrida
conserva, por cada parámetro divergente: el valor aplicado, el valor que el
sistema había derivado, la magnitud de la diferencia, el instrumento que fija el
valor aplicado y la evidencia del valor derivado.

No bloquea nada. No genera QC predial. Es visible para el técnico municipal y
para cualquier auditoría posterior.

`[C]` **Limitación intrínseca:** solo hay divergencia registrable donde el
sistema **tiene** un valor derivado. Para los parámetros no derivables del
corolario 2 de D1 —`Vz`, `Tip`, toda cifra monetaria— no existe término de
comparación y no hay divergencia que registrar, solo adopción. El registro cubre
los parámetros metodológicos, que son la mayoría, pero no los que más pesan sobre
la base imponible.

### D8 — Corrección terminológica de los ADR anteriores

Donde ADR-0068, ADR-0069 y ADR-0070 dicen **"ordenanza"**, debe entenderse **Ley
Municipal**, denominada **"Ley Autonómica Municipal"** en Uyuni.

**Los ADR aceptados no se reescriben.** La corrección se declara aquí y los tres
documentos se leen a través de ella, conforme a la convención append-only de la
serie. `[C]` "Ordenanza Municipal" es la denominación anterior al marco
autonómico vigente; su uso en aquellos ADR es un error de denominación, no de
sustancia: en los tres casos se quiso decir *instrumento normativo municipal
competente*.

### D9 — El vocabulario de códigos municipales requiere mapeo versionado, no un documento

ADR-0070 declaró E3 —el diccionario semántico de `uso_terreno`— como tercera
dependencia externa del GAM. Bajo el alcance de D1 esa formulación queda corta.

`[C]` **Cada GAM va a traer su propio vocabulario de códigos de uso, y ninguno
va a traer diccionario.** El catastro heredado de Uyuni usa `VIV`, `TRR`, `SIN`,
`COM`, `SER`, `TRU`, `EDU`, `OFI`, `DEP`, `SAL`, `REC`, `IND`, `REL`, `CMC`,
`CUL`; otro municipio usará otros. E3 no es un documento faltante en un municipio:
es una **clase de dependencia** que se repetirá en cada incorporación.

**Decisión:** el sistema incorpora un **mecanismo de mapeo de vocabulario
versionado por municipio**, que traduce los códigos de origen a las categorías
que la norma nacional define. El mapeo es dato del GAM y sigue el mismo circuito
de aprobación de D4.

`dominio.catalogo_uso_suelo` ya contiene quince categorías normalizadas y es
candidato natural al lado de destino del mapeo. `[V]`
`dominio.predios.uso_suelo_id` está poblado en 0 de 11.985 filas: el catálogo
existe y el enlace no.

---

## Consecuencias

- **M016 no cambia por este ADR**, y eso queda confirmado: el DDL crea estructura
  vacía, multi-municipio, sin ningún valor cargado. Lo que este ADR aporta al DDL
  son tablas —instrumento normativo, historial de estados, registro de
  divergencia— y una prohibición: *Vigente* no es columna.
- **Los quince parámetros `[C]` de ADR-0068 y ADR-0070 cambian de estatus**, no de
  valor. Dejan de ser propuestas para Uyuni y pasan a ser valores por defecto
  derivados, sobrescribibles por instrumento municipal.
- `requiere_validacion_oficial = true` de ADR-0070 D2 se implementa a través de
  D4: un conjunto en `PropuestaTecnica` calcula previews y no emite.
- **La incorporación de un municipio nuevo tiene ahora un procedimiento
  declarado:** cargar cartografía, derivar los parámetros derivables sobre sus
  datos, dejar vacíos los monetarios, mapear su vocabulario, y esperar
  instrumento.
- `[D]` **El caso de Uyuni deja de ser especial.** Si la Ley 090/2024 existe y
  cubre estos parámetros, Uyuni es simplemente un municipio que ya recorrió el
  circuito, y sus valores entran por el mismo camino que los de cualquier otro.

---

## Limitaciones declaradas

### L1 — Las citas jurídicas de D5 y D6 no fueron verificadas

`[D]` Ley 482 arts. 13, 23 y 26.17; CPE art. 123; Código Tributario arts. 3 y
150. Provienen del orquestador. **Ninguna fue leída en su fuente durante esta
sesión** y el equipo técnico no tiene competencia para interpretarlas. Deben
confirmarse con quien redacte el instrumento municipal antes de que este modelo
sostenga una emisión tributaria.

### L2 — El registro de divergencia no cubre los parámetros que más pesan

Documentado en D7. `Vz` y `Tip` no tienen valor derivado contra el cual comparar.

### L3 — El modelo del instrumento se diseñó sin haber visto ninguno

`[V]` No hay ningún instrumento normativo municipal en disco. Los campos de D5
provienen de la estructura declarada de la Ley 482, no del examen de un documento
real. **El primer instrumento que se cargue va a poner a prueba este modelo**, y
es esperable que falte algún campo.

### L4 — La frontera de D1 tiene casos de borde no resueltos

`[C]` D2 de ADR-0066 decidió que rige el Capítulo IV de RM 024/2024 frente al
Capítulo VI, con un efecto del orden del 10% sobre la base imponible. Bajo D1 esa
elección es **interpretación de la norma nacional**, o sea código. Pero un
municipio podría adoptar el Capítulo VI por instrumento propio, y entonces sería
dato. **No está decidido de qué lado cae**, y el caso no es hipotético: la
contradicción está en la norma y cualquier GAM puede zanjarla distinto.

---

## Criterios de diseño, no consecuencias de la medición

Este ADR es casi enteramente criterio. Se listan los puntos donde un lector
futuro podría decidir de otro modo sin contradecir ninguna evidencia:

La frontera exacta de D1 · derivar *Vigente* en lugar de almacenarlo · aprobar
por conjunto en lugar de por parámetro · permitir previews con conjunto
incompleto · el modelo de campos del instrumento · registrar la divergencia sin
bloquear · el nivel de corrida para ese registro · mapear vocabulario en lugar de
exigir diccionario.

Dos merecen fundamento explícito:

**Derivar *Vigente*.** Almacenarlo es más simple de consultar y más rápido. El
argumento en contra es que introduce un proceso programado cuyo fallo es
silencioso, y que destruye la vigencia histórica necesaria para reproducir
corridas pasadas. La reproducibilidad pesó más que la simplicidad de consulta.

**Registrar la divergencia sin bloquear.** El sistema es una herramienta del GAM
y de la población, no un contralor de la autoridad municipal. Pero una
herramienta que no deja constancia de lo que midió no protege a nadie cuando un
avalúo se impugna. El registro es el equilibrio entre ambas cosas, y su costo es
que produce información que nadie está obligado a mirar.

---

## Pendiente

1. **Verificar las citas jurídicas de D5 y D6** con quien redacte el instrumento
   municipal (L1).
2. **Resolver el caso de borde del Capítulo IV / Capítulo VI** (L4): si la
   elección de capítulo es código o dato del GAM.
3. **Definir el conjunto cerrado de `naturaleza_normativa`.** Hoy solo se
   conocen dos valores plausibles y el sistema es nacional.
4. **Auditar, cuando se obtengan, la Ley 090/2024 de Uyuni, su decreto
   reglamentario y la tabla CVZ**, con identidad por hash. No bloquean este ADR
   ni M016; determinarán qué valores carga Uyuni.
5. **Reconciliar el motor contra la emisión de IMPBI 2024 de Uyuni**, si el
   padrón emitido está disponible. `[C]` Sería la validación externa más fuerte
   al alcance: contrastar la salida del motor contra lo que el municipio
   efectivamente cobró.
6. **Ejecutar la consulta de `IPES` por zona** para determinar si el sistema
   puede derivar un valor por defecto con la cartografía disponible.
7. **Diseñar el mecanismo de mapeo de vocabulario de D9** con suficiente detalle
   para M016.
