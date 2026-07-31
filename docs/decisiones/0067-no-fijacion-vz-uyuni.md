# ADR-0067 — No se fija valor zonal imponible para Uyuni con la evidencia disponible

- **Estado:** Aceptada.
- **Fecha:** 2026-07-29 · Fase 4.A.
- **Naturaleza:** determinación **time-bound**. Quedará superseded cuando exista
  una campaña de encuestas conforme al Cap. IV de RM 024/2024.
- **Depende de:** ADR-0066 (motor y cadena de derivación).
- **Corrige:** D6 de ADR-0045 en su atribución metodológica.
- **Evidencia:** `scripts/auditoria_vz.py`, SHA-256
  `A4C28E4D773DE0EEA10DC4B70E0BB3C6769A086A250EFE100B901A95832A09B2`,
  450 líneas, 15 secciones. Reproducción: ver §Reproducción al final.

---

## Contexto

ADR-0045 D6 fijó `Vz(zona C) = 88 Bs/m²` para Uyuni, derivado de 198 encuestas
por "mediana de las medianas por manzana", declarándolo el método de autoavalúo de
la Guía 2024.

La auditoría reproducible de esas 198 encuestas contra los archivos fuente
establece tres cosas: el método declarado no es el de la norma, el valor no se
reproduce por el método declarado, y los datos admiten una banda de valores
demasiado amplia para fijar una base imponible.

---

## Decisiones

### D1 — No se fija ningún `Vz` imponible para Uyuni

**Decisión:** el catálogo de valores zonales nace **vacío**. El motor de ADR-0066
se entrega completo y sin datos de valor. Ningún `Vz` se declara tributable hasta
que exista campaña conforme al Cap. IV.

Fundamento cuantitativo, todo `[V]` reproducible:

**a) La banda defendible es de un factor de dos.** Sobre 123 observaciones
admitidas en 42 manzanas de la zona C:

| Criterio | `Vz(A)` promedio de promedios | `Vz(B)` mediana de medianas |
|---|---|---|
| todas las admitidas | 127,28 | 70,86 |
| manzanas con n≥2 | 145,80 | 83,84 |
| manzanas con n≥3 | 133,92 | 74,44 |
| manzanas con n≥4 | 129,46 | 73,29 |
| excluyendo filas incoherentes | 136,02 | 91,67 |
| solo confianza ALTA | 153,87 | 90,00 |
| terreno = total − construcción | 145,16 | 83,35 |

La banda va de **≈71 a ≈154 Bs/m²**. Ninguna de las opciones que la producen está
prescrita: la norma no dice qué hacer con manzanas de observación única, ni con
declaraciones que se contradicen, ni fija el estadístico más allá de "promedio".
**El dato admite una banda de 2× y la norma no elige dentro de ella.**

**b) El instrumento se contradice consigo mismo.** Cinco predios fueron
encuestados dos veces:

| Triplete | Encuesta A | Encuesta B | Razón |
|---|---|---|---|
| 3-29-1 | 74,27 | 320,93 | **4,32×** |
| 5-6-11 | 35,44 | 106,32 | 3,00× |
| 3-29-8 | 81,78 | 163,56 | 2,00× |
| 3-29-11 | 113,42 | 185,18 | 1,63× |
| 5-76-17 | 67,65 | 72,72 | 1,08× |

Razón mediana 2,00×. No se puede fijar una base imponible a dos cifras
significativas con un instrumento que se contradice al 100% sobre el mismo objeto.

**c) Las declaraciones no son independientes.** 198 observaciones contienen solo
**83 valores distintos**. Un único monto —70.000 Bs— concentra el **17,7%**. El
46% son múltiplos exactos de 10.000 Bs y el 62% de 5.000. Ese monto único,
aplicado sobre 35 superficies de 120,5 a 787 m², genera por pura aritmética un
rango de 88,95 a 580,77 Bs/m² (6,5×, CV 53%). El método de autoavalúo de la Guía
presupone declaraciones independientes por predio; **esa premisa no se cumple**.

**d) 24% de las observaciones admitidas provienen de filas que se contradicen.**
46 de 198 encuestas fallan `terreno + construcción = total` (23,2%), y 30 de las
123 admitidas están entre ellas. Tres casos declaran un total **menor que el
terreno solo**, de modo que una de las dos cifras es necesariamente falsa:

| Registro | Terreno | Construcción | Suma | Total declarado | Bs/m² resultante |
|---|---|---|---|---|---|
| 22 | 90.000 | 20.000 | 110.000 | 40.000 | 115,20 |
| 107 | 48.000 | 0 | 48.000 | 18.000 | 110,76 |
| 183 | 300.000 | 35.000 | 335.000 | 105.000 | **1.001,29** |

El registro 183 sostiene el promedio de manzana más alto de todo el conjunto
(1-72, 735,57 Bs/m²). La cima de la distribución descansa sobre una fila que se
contradice a sí misma.

**e) La cobertura territorial incumple el diseño.** La zona C tiene 149 manzanas.
El Cap. IV exige 2 predios en el 50% de ellas: 75 manzanas, 149 predios. Hay 42
manzanas (56%) y 123 predios (83%), con asignación desigual: 10 manzanas con una
sola observación y solo 7 con las dos que pide el diseño. Los dos extremos que
producen la razón de 42× entre promedios de manzana salen de celdas con n=1
(mínimo 17,37) y n=2 (máximo 735,57).

**Cobertura y calidad no son separables con esta evidencia**: menos manzanas
implica mayor peso de cada promedio y por tanto mayor dispersión agregada.

### D2 — Los 88 Bs/m² quedan como estimación histórica no tributable

`Vz(A)` sobre la misma base da 127,28; el método declarado en D6 da 70,86. El
valor 88 solo se aproxima excluyendo el distrito 1 (87,33), exclusión **sin
fundamento normativo alguno**, o bajo la variante total−construcción (83,35).

**Decisión:** 88 Bs/m² se registra como **estimación histórica no tributable**, con
el mismo tratamiento que ADR-0045 D7 dio a los 374 Bs/m² de construcción. No se
afirma que sea reproducible por el método declarado, porque no lo es. Se afirma
únicamente que cae dentro de la banda de variantes plausibles.

### D3 — El crosswalk encuesta→predio queda declarado provisional

`crosswalk_encuesta_predio.csv` es un derivado de `resolver_encuestas.py`, que
resuelve por proximidad de superficie con `TOLERANCIA_ABS = 10.0 m²` y
`EMPATE_MAX = 0.5 m²`, constantes sin fundamento documentado.

El filtro zonal detectó **16 falsos positivos sobre 139 resoluciones = 11,5%**.
Es una **cota inferior**: los falsos positivos que caen dentro de la propia zona C
son indetectables por este control.

**Decisión:** el crosswalk es provisional y sus constantes quedan sin ratificar.
No se corrigen: se vuelven innecesarias. La geo-captura de D9 de ADR-0045 resuelve
el vínculo por join espacial, sin tolerancias.

### D4 — La zonificación es entregable de la fase, no insumo heredado

Las zonas A–D vigentes no delimitan áreas económicamente homogéneas. Dentro de la
zona C, por distrito:

| Distrito | Manzanas | Obs. | `Vz(A)` | CV interno |
|---|---|---|---|---|
| 1 | 3 | 10 | 646,68 | 73% |
| 3 | 13 | 41 | 120,95 | 51% |
| 5 | 14 | 43 | 68,95 | 102% |
| 6 | 12 | 29 | 72,35 | 94% |

Gradiente de 9× dentro de una supuesta zona homogénea. El distrito 1 es el centro
urbano.

**Advertencia contra la solución aparente:** cambiar la llave del catálogo a
`(distrito, zona)` **no** resuelve el problema — el CV interno permanece entre 73%
y 102%. Ninguna partición de estos datos produce un grupo homogéneo, porque el
piso de ruido lo impone la encuesta, no la delimitación.

**Decisión:** la delimitación de zonas homogéneas se trata como entregable, a
construir desde cartografía según los Caps. II y III de RM 024/2024. Pero eso no
sustituye datos nuevos: rezonificar sobre las mismas 198 encuestas no produce un
valor defendible.

---

## Dependencias externas del GAM

Ambas son competencia municipal y ninguna está en manos del equipo de desarrollo.

| # | Dependencia | Destraba |
|---|---|---|
| **E1** | Campaña de encuestas con el diseño del Cap. IV (2 predios en el 50% de las manzanas de cada zona) **y** el control de calidad de D9 de ADR-0045: validación aritmética antes de guardar, catálogos en lugar de texto libre, geo-captura GPS | `Vz` de todas las zonas · Fase A completa |
| **E2** | Estudio municipal de valores Bs/m² por subtipo constructivo, aprobado por ordenanza | `Tip` · Fase B |

E1 no es una tarea de transcripción. Los defectos (b), (c) y (d) de D1 son
exactamente los que D9 de ADR-0045 previene por diseño: el ruido de 2×, la
concentración en montos convencionales y la incoherencia aritmética desaparecen
con validación en captura. **La inversión en el colector es lo que hace posible el
valor fiscal**, no un accesorio.

---

## Consecuencias

- La Fase 4.A entrega **motor conforme a norma con catálogo vacío**, no "Uyuni
  valuado". Es menos vistoso y es lo único que sostiene una auditoría de concejo.
- Las cifras del demo de la exposición **no son reutilizables**.
  `DEMO_valuacion_terreno_zonaC.csv` calculó `Vt = SupT × 88` sin cadena zonal y
  sin coeficientes. Omitió la cadena (≈×1,8 al alza) y los coeficientes (≈×0,5 a
  la baja) y ambos errores casi se cancelaron. La coincidencia es fortuita.
  `[C]` Estimación: sobrestima entre 10% y 60% según qué `VPz` se tome; usa `Fs`
  promedio municipal y un `IPES` no medido contra el inventario real de
  equipamientos.
- R1 de ADR-0045 está **al 70%, no pendiente**: 139 de 198 encuestas ya tienen
  distrito recuperado computacionalmente. Faltan 59, y buena parte es
  irrecuperable por método: los empates son lotes estándar idénticos de 437,5 m²
  en distritos distintos, con diferencias sub-milimétricas. Solo el formulario los
  distingue.
- Las 198 encuestas contienen, en texto libre, **más de lo que ADR-0045 supone**:
  `Tipo_Edificacion` trae ubicación en manzana (esquina/central → `Fum`) y
  estándar constructivo (económico/normal/muy bueno → subtipo de la Tabla A.1), y
  `Observaciones` trae material de vía. Con 198 filas es extraíble a mano y
  permite probar la fórmula completa sobre un subconjunto real.

---

## Reproducción

Toda cifra de este ADR se reproduce con:

```
python scripts/auditoria_vz.py --emit-sql
# ejecutar la consulta emitida y guardar la salida en predios_zona.txt

python scripts/auditoria_vz.py \
  --encuestas ENCUENTAS_FINAL_198_REGISTROS_CORREGIDO_21_70.csv \
  --crosswalk crosswalk_encuesta_predio.csv \
  --predios   predios_zona.txt \
  --zona C
```

El script imprime las reglas de admisión, el embudo fila por fila con motivo de
cada descarte, y los resultados intermedios de las 15 secciones. La consulta SQL
la emite el propio script, de modo que las reglas de cálculo y la consulta no
pueden divergir.

Controles cruzados independientes verificados: manzanas de la zona C = 149 por dos
vías distintas; filas del crosswalk con `COT_CAT` no vacío = 139 por conteo
externo. Sin discrepancias.

**Advertencia de procedencia:** todas las cifras heredan las dos constantes no
fundamentadas de `resolver_encuestas.py` (D3).
