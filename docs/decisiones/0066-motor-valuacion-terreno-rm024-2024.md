# ADR-0066 — Motor de valuación de terreno conforme a RM 024/2024

- **Estado:** Aceptada.
- **Refinada por:** ADR-0068 (calibración de los umbrales de `Ff`, D5).
- **Desarrollada por:** ADR-0069 (modelo de datos de valuación).
- **Fecha:** 2026-07-29 · Fase 4.A.
- **Relación con ADR-0045:** lo **corrige y complementa**; no lo supersede. D1, D5,
  D7 y D8 de ADR-0045 permanecen ratificadas. D4 y D6 se corrigen aquí.
- **Naturaleza de la sesión:** análisis y decisión. Sin cambios al repositorio, a
  la base ni a los datos fuente. Toda la evidencia se levantó en modo lectura.
- **Evidencia base:** *Guía Nacional de Zonificación y Valuación Zonal*, aprobada
  por **Resolución Ministerial N° 024 del 1° de febrero de 2024** (75 páginas, PDF
  nativo, capa de texto verificada); auditoría reproducible de las 198 encuestas
  de Uyuni (`scripts/auditoria_vz.py`, SHA-256
  `A4C28E4D773DE0EEA10DC4B70E0BB3C6769A086A250EFE100B901A95832A09B2`, 450 líneas); consultas
  de esquema y distribución sobre la versión activa de Uyuni (`051201`).

---

## Contexto

ADR-0045 fijó la fórmula de valuación de terreno como
`Vt = SupT · Vz · Fs · Fi · Ff · Fum` sin poder citar su fuente: la norma que la
contiene no estaba disponible en esa sesión. Se recomendó reescribirlo por falta
de fundamento normativo.

Esa recomendación era incorrecta. La *Guía Nacional de Zonificación y Valuación
Zonal* (RM 024/2024) contiene la fórmula de forma literal, variable por variable,
en su página 37, y la de construcción en su página 48. ADR-0045 fue escrito desde
esta norma. Lo que le faltaba no era la fórmula sino **las tablas de coeficientes
y la cadena de derivación del valor zonal**, ausentes porque el documento no
estaba a la vista.

Este ADR incorpora la norma, corrige los dos defectos que su ausencia produjo, y
fija los parámetros que la propia norma delega en el municipio.

---

## Decisiones

### D1 — Régimen normativo aplicable: autoavalúo, no catastral

Existen **dos regímenes legales distintos**, no tres instrumentos compitiendo por
la misma materia:

| Régimen | Fundamento | Fórmula | Techo del 85% |
|---|---|---|---|
| Catastral / valor fiscal | DS 22902 (1991) + Guía Nacional de Catastro Urbano | `Vt = A·Vz·K1·K2·K4·K5·K6` | aplica |
| **Autoavalúo** | **Ley 843 art. 55 + RM 024/2024** | `Vt = SupT·Mv·Fs·Fi·Ff·Fum` | **no aplica** |

RM 024/2024 no contradice al DS 22902: ocupa el espacio que el art. 55 de la Ley
843 deja abierto mientras no se practiquen avalúos fiscales. Su alcance (pp. 3-4)
la declara *de uso y cumplimiento obligatorio* para los GAM que aún no derivaron
su modelo de valoración de un catastro implementado.

**Decisión:** el sistema implementa el **régimen de autoavalúo** como camino
primario. Uyuni y Caranavi califican: ninguno figura entre los municipios con
normativa u oficina de catastro que la propia Guía enumera (p. 4).

El motor conserva el versionado de fórmulas que D4 de ADR-0045 ya decidió
(`valuacion.formula_version`), con RM 024/2024 como versión preferida.

### D2 — Capítulo IV como capítulo rector

RM 024/2024 es **internamente contradictoria**. La misma fórmula aparece en tres
versiones y la tabla de inclinación en dos:

| Ubicación | Fórmula | Tabla de inclinación |
|---|---|---|
| Cap. IV, p. 37 | `SupT · Mv · Fs · Fi · Ff · Fum` | 5 tramos: 1,10 / 1,00 / 0,90 / 0,60 / 0,50 |
| Cap. VI, pp. 49-50 | `SupT · Mv · Fs · Fi · Fu` — **sin `Ff`** | 3 tramos: 1,00 / 0,90 / 0,80 |
| Flujo, p. 51 | `SupT · Mv · Fs · Fi · Ff · Fum` | — |

La discrepancia no es académica: `PLA` cubre el 98% de los predios de Uyuni
(11.731 de 11.985) y vale **1,10 bajo Cap. IV** contra **1,00 bajo Cap. VI**. La
elección de capítulo mueve la base imponible municipal alrededor del 10%.

**Decisión:** rige el **Capítulo IV**, por ser el desarrollo metodológico; el
Cap. VI se interpreta como resumen operativo con errores de transcripción (remite
además el cálculo de construcción al "capítulo IV" cuando es el V). La
contradicción queda documentada aquí para que la ordenanza municipal la cite
explícitamente y no quede expuesta en una impugnación.

### D3 — Cadena completa de derivación del valor zonal · **corrige D6 de ADR-0045**

ADR-0045 colapsó cuatro pasos normativos en una sola variable `Vz`. La norma
prescribe:

| Paso | Fórmula | Página |
|---|---|---|
| 1 | `VPz` = promedio de los promedios por manzana | 33 |
| 2 | `VSz = VPz · ((IPIU + IPES + IPRT) / 3)` | 33 |
| 3 | `Mv = VSz · IPV` | 36 |
| 4 | `Vt = SupT · Mv · Fs · Fi · Ff · Fum` | 37 |

El multiplicando de la fórmula es **`Mv`**, no `VPz`. Los 88 Bs/m² de ADR-0045
son un `VPz` — el valor crudo de encuesta — colocado donde la norma exige `Mv`.
Con el perfil real de la zona C de Uyuni el factor omitido es del orden de 1,25 en
vía de tierra y 1,44 en vía pavimentada.

**Decisión:** el modelo de datos representa los cuatro eslabones por separado. Se
prohíbe almacenar un único "valor de zona" sin declarar a qué eslabón corresponde.

Coeficientes tabulados que se adoptan (`[V]`, RM 024/2024):

- **IPIU** (p. 34) = 1,00 + 0,20 por cada uno de: agua potable, energía eléctrica,
  alcantarillado, otros servicios. Máximo 1,80.
- **IPES** (p. 34) = 1,00 + 0,05 por **categoría presente** (no por cantidad de
  equipamientos): salud, cultural-educativo-recreativo, comercial, gestión,
  transporte. Máximo 1,25.
- **IPRT** (p. 34): accidentado o anegadizo 1,00 · pendiente alta 16°–45° 1,05 ·
  pendiente baja 11°–15° 1,10 · plano 0°–10° 1,20.
- **IPV** (p. 36): tierra 1,00 · ripio 1,05 · piedra 1,10 · loseta / cemento /
  adoquín 1,15 · asfalto / pavimento rígido 1,20.
- **Fs** (p. 37) = 0,20 mínimo + 0,20 por servicio disponible en la vía. Máximo 1,00.
- **Fi** (p. 37, Cap. IV): plano 0°–5° 1,10 · semiplano 5°–10° 1,00 ·
  pendiente 10°–20° 0,90 · muy pendiente 20°–40° 0,60 · barranco >40° 0,50.
- **Ff** (p. 37): regular 1,00 · irregular 0,90 · muy irregular 0,80.
- **Fum** (p. 37): esquina 1,20 · medio 1,00 · al interior 0,80.
- Áreas de expansión urbana (p. 37): 20% del valor más bajo del centro urbano.

### D4 — `SupT = superficie_sig`

`dominio.predios.superficie_sig` está poblada en **11.985 de 11.985** filas de
Uyuni; `superficie_oficial` en 0. La norma exige información geométrica que sea
*fiel reflejo de la realidad* (p. 9).

**Decisión:** `SupT = superficie_sig`, con `superficie_oficial` como override
cuando exista. Coherente con D8 de ADR-0045, ya ratificada ("el área GIS es el
área de registro"). No se usa `superficie_declarada` para el cómputo.

### D5 — `Ff` se deriva de **solidez**, no de Polsby-Popper · **corrige D6 de ADR-0045**

ADR-0045 derivó `Ff` de la compacidad de Polsby-Popper. **Polsby-Popper mide la
propiedad equivocada**: confunde elongación con irregularidad. Demostración
aritmética verificable a mano:

| Forma | Polsby-Popper |
|---|---|
| Rectángulo 1:1 (cuadrado) | 0,7854 |
| Rectángulo 1:2 | 0,6981 |
| Rectángulo 1:4 — **perfectamente regular** | **0,5027** |
| Forma en L (10×10 menos 5×5) — **irregular** | **0,5890** |

Un lote en L obtiene mayor compacidad que un rectángulo perfecto de fondo 1:4.
La distribución observada en la zona C de Uyuni (2.725 predios) lo confirma:
mediana 0,6092 ≈ rectángulo 1:2,8; p25 0,5578 ≈ 1:3,2; p75 0,6816 ≈ 1:2,1. No es
una distribución de irregularidad, es la distribución de fondos de un loteo
rectangular normal. Usar PP para `Ff` penalizaría los lotes profundos corrientes
y premiaría los realmente irregulares.

**Decisión:** `Ff` se deriva de la **solidez** `ST_Area(g) / ST_Area(ST_ConvexHull(g))`,
que vale 1,00 para cualquier rectángulo sin importar su proporción y 0,75 para la
forma en L del ejemplo. Los umbrales que separan 1,00 / 0,90 / 0,80 son
**parámetro municipal versionado**, no constante del código, y se fijan por
ordenanza sobre la distribución medida del municipio.

`[C]` Pendiente de medición: la distribución de solidez de Uyuni no se levantó. Si
resultara concentrada en 1,00 —plausible en un loteo regular— `Ff` sería
constante en la práctica y correspondería documentarlo antes que aplicarlo.

### D6 — Los coeficientes son **datos versionados por municipio**

RM 024/2024 delega en el GAM la fijación de valores y admite dos tablas
incompatibles para el mismo factor (D2). Un municipio distinto puede adoptar el
Cap. VI, o umbrales de solidez distintos, por ordenanza propia.

**Decisión:** todos los coeficientes se modelan como **datos**, versionados por
municipio y por versión de fórmula, no como constantes compiladas. La llave del
catálogo de valores zonales es **`(municipio_codigo, nombre_zona)`**.

Fundamento de la llave: las zonas de Uyuni son geométricamente disjuntas — la
zona C son 3 polígonos (`id_zona_origen` 3, 4 y 9) y la D son 4 (5 a 8), sobre 4
nombres distintos. El valor se adhiere a la **zona homogénea nombrada**, no al
polígono; `id_zona_origen` es identificador de polígono.

### D7 — Doble cómputo de servicios y topografía: se replica y se documenta

La norma aplica servicios dos veces (`IPIU` a nivel zona, `Fs` a nivel predio) y
topografía dos veces (`IPRT` y `Fi`). Con `Fs` mínimo en 0,20 y el 32% de los
predios de Uyuni sin ningún servicio (3.819 de 11.985), el efecto se acumula: el
`Fs` promedio ponderado del municipio es ≈ 0,48.

**Decisión:** se **replica la norma tal cual**, incluido el doble cómputo, y se
documenta como defecto normativo conocido. Corregirlo por criterio propio
expondría al GAM a impugnación por apartarse del instrumento aplicable.

Regla adicional: `NULL` **no** es `NO`. 245 parcelas de Uyuni tienen los cuatro
servicios en `NULL`. Un `NULL` bloquea el cálculo y marca el predio como
*observado*; no se le imputa ausencia de servicio.

### D8 — Identificador: la tripleta es el código prescrito, no herencia

RM 024/2024 codifica **Zona → Manzana → Lote** (pp. 26-28), con numeración
determinista: zona central 01, luego la del norte, espiral horaria; manzana
central de la zona 001, misma regla; lote 001 el más al norte y al este.

**Decisión:** la tripleta `(cod_uv, cod_man, cod_pred)` es el **identificador
prescrito por el régimen aplicable**, no un código heredado a reemplazar. El
geocódigo UTM pertenece al régimen catastral y queda disponible como
identificador secundario, no como llave de negocio. Ratifica D2 y D3 de ADR-0045.

---

## Consecuencias

- La Fase A del motor queda **completamente especificada** y todos sus
  coeficientes son computables desde columnas ya pobladas: topografía
  (`topografia_terreno`, 98% `PLA`), servicios (4 columnas booleanas), material
  de vía (`capa_vias.material`, 4 de los 8 materiales de la norma presentes),
  forma (geometría) y ubicación en manzana.
- **Falta un insumo, no una decisión:** el valor zonal. Ver ADR-0067.
- `Mv` por predio **exige asignación espacial predio → tramo de vía**: no existe
  clave entre `predios` y `capa_vias`. La norma prescribe exactamente esa
  individualización por tramo de manzana (pp. 22 y 28). Es trabajo de
  implementación, no un lookup. Impacto acotado: 90% de las vías de Uyuni son de
  tierra (`IPV` = 1,00), pero el 10% pavimentado es el centro.
- La Fase B (construcción) sigue bloqueada por un solo insumo, confirmado ahora
  por segunda fuente: RM 024/2024 trae la Tabla A.1 completa con 6 subtipos de
  "Casa" y las tablas de antigüedad y conservación idénticas al RNCU. La única
  celda vacía es **Bs/m² por subtipo**, que la norma remite a estudio municipal.
  Ratifica el bloqueo de D4 de ADR-0045.
- **Supuesto de diseño del producto:** los catastros heredados llegan con los
  campos económicos presentes y vacíos. En Uyuni, `val_zon` es inutilizable (D5
  de ADR-0045, ratificada) e `imp_pag` es 0 en 11.982 de 11.985 filas. No existe
  línea base tributaria en los datos. El producto debe construirse para **poblar**
  esos campos, no para leerlos.

---

## Pendiente de cierre

1. ~~Medir la distribución de solidez y fijar los umbrales de `Ff` (D5).~~
   **Cerrado por ADR-0068.**
2. Diseñar M016 sobre esta especificación: catálogo de coeficientes por municipio
   y por versión de fórmula, con la llave de D6.
3. ~~Evaluar si D5 y D6 justifican un ADR separado para el modelo de datos.~~
   **Cerrado por ADR-0069.**
