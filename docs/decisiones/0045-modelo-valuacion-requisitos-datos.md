# ADR-0045 — Modelo de valuación catastral y requisitos de datos (piloto Uyuni)

- **Estado:** Propuesta (borrador para revisión y aprobación de Saul).
- **Fecha del análisis:** 2026-06-16 · Sprint 4, día 5.
- **Reconstruido:** 2026-06-17 (ver "Nota de reconstrucción" más abajo).
- **Naturaleza de la sesión:** análisis y diseño. NO hubo ejecución, importación ni cambios al repo/base canónica. El análisis espacial fue read-only sobre geodata en `C:\Proyectos\SG_SAUL_CATASTRO_DATOS` (fuera del repo).
- **Supersede:** borrador `DISENO_VALUACION_Y_REQUISITOS_DATOS.md` (lo absorbe y amplía).
- **Evidencia base:** 11.985 predios (`PRE_SIS_UYU`), 198 encuestas de valuación (CSV), 7 capas SHP de Uyuni (EPSG:32719), formulario IGM (vacío y lleno), y `ESPECIFICACION_TECNICA_SOFTWARE_CATASTRO_BOLIVIA.md`.

---

## Nota de reconstrucción (leer primero)

Este archivo fue **reconstruido a partir de la conversación de diseño del 2026-06-16**, porque el `.md` original (`ADR-XX_modelo_valuacion_y_requisitos_datos.md`) se generó en una sesión cuya carpeta de salida ya no es accesible. La reconstrucción distingue tres niveles de confianza:

- **FIRME (ratificada).** Las cuatro decisiones que Saul aprobó explícitamente conservan su número original: **D1, D5, D7, D8**.
- **FIRME (contenido).** Todo el contenido técnico, las fórmulas, la tabla R1–R5, los requisitos del colector, los hallazgos de calidad y el anexo cuantitativo se recuperaron casi literalmente y son confiables.
- **RECONSTRUIDA (numeración a verificar).** Las cinco decisiones restantes (**D2, D3, D4, D6, D9**) recuperan su *contenido* con confianza, pero **el número/etiqueta exacto que tenían en el archivo original no es verificable**. Si reaparece el `.md` original (en Descargas o en `...DATOS`, fuera del repo), reconciliar la numeración de estas cinco contra él.

> **Numeración de la serie:** se asignó **0045** asumiendo que **0044 (AuditoriaInterceptor) es el ADR de número más alto existente**. Confirmar contra `docs/decisiones/`; si hay un número mayor, correr este a máximo+1. Los huecos de la serie (0002–0004, 0008–0010, 0020–0024) **no se rellenan** (los ADR son monotónicos/append-only).

---

## Contexto

El sistema debe valuar predios urbanos conforme a la normativa boliviana (Guía Nacional de Zonificación y Valuación Zonal 2024; Reglamento Nacional de Catastro Urbano 1991). La sesión analizó los datos reales de Uyuni para responder: ¿qué información exacta necesita el motor para valuar, qué es computable hoy y qué está bloqueado por falta de dato o de norma?

El motor normativo es:

```
Vi = Vt + Vc
Vt = SupT · Vz · Fs · Fi · Ff · Fum      (terreno  — Fase A)
Vc = SupC · Tip · Fa · Fe                 (construcción — Fase B)
```

Donde `Vz` = valor de zona (Bs/m²); `Fs` servicios; `Fi` inclinación/topografía; `Ff` forma del predio; `Fum` uso de suelo/ubicación; `Tip` valor Bs/m² por tipología; `Fa` factor antigüedad; `Fe` factor estado de conservación.

---

## Decisiones

### D1 — `ubic_zona` → `distrito` · **RATIFICADA**
`ubic_zona` en la base canónica contiene, casi con certeza, el **distrito** (1–6), no la zona-letra. Se renombrará a `distrito`.
**Ratificación de Saul:** para la **exposición** se presenta correctamente como distrito; el **renombrado real en la base se difiere a la implementación final** (queda agendado, no se ejecuta en demo). Previo al rename real, verificar contra la base canónica (no tocada en esta sesión).

### D2 — Llave canónica del predio · **RECONSTRUIDA (numeración a verificar)**
La llave única del predio es el triplete **`(cod_uv, cod_man, cod_pred)`** = `COT_CAT` = `id_predio`. **`cod_uv` ES el distrito (1–6)**, no "unidad vecinal". Verificado con ground-truth: predio `3-2-24` → 300 m² ✓, y unicidad confirmada en los 11.985 registros.

### D3 — Zona y distrito son particiones CRUZADAS · **RECONSTRUIDA (numeración a verificar)**
Zona-letra y distrito **no están anidados**: son particiones cruzadas. Por eso `(zona, manzana, predio)` es **ambiguo (6.021 duplicados)** y no puede recuperar el distrito. La zona-letra cuelga del predio **solo por geometría (sin FK)**, se obtiene por **join espacial**, no es única y solo existen {A, B, C, D} reales. La llave de negocio debe llevar **siempre** el distrito.

### D4 — Motor de valuación por fases + versionado de fórmula · **RECONSTRUIDA (numeración a verificar)**
- **Fase A (terreno, `Vt`): ~70% computable hoy.** Superficie, servicios, vía, topografía y uso están poblados al 97–100%. Faltan dos cosas: la **forma `Ff`** (derivable de geometría) y el **valor de zona `Vz`**, que *no es un dato almacenado* — se **calcula desde las encuestas** (ver D6).
- **Fase B (construcción, `Vc`): NO computable hoy.** Faltan `Fa` (año), `Fe` (estado de conservación) y `Tip` (Bs/m² por tipología). La norma **prohíbe inventar `Tip`**: exige estudio municipal con ordenanza (ver R3, R4).
- El motor **versiona fórmulas** (`valuacion.formula_version`): Guía 2024 (preferida, fórmula simple, sin ajuste 0.85) vs Reglamento 1991 (K1–K6). No se elige una "a fuego".

### D5 — `val_zon` se descarta para el cómputo · **RATIFICADA**
La columna `predio.val_zon` (18 valores; moda 10; rango 5–22; 117 ceros) **no contiene un valor de zona usable** (la fuente IGM confirmó que no tiene valor real). `Vz` **no** se deriva de `val_zon`.
**Ratificación de Saul:** se **mantiene el nombre de columna `val_zon`** (no se renombra), pero su contenido se descarta para el cálculo.

### D6 — `Vz` derivado de encuestas (autoavalúo Guía 2024) + `Ff` por compacidad · **RECONSTRUIDA (numeración a verificar)**
- **`Vz` se calcula desde las 198 encuestas** por el método de autoavalúo de la Guía 2024: **mediana de las medianas por manzana** (38 manzanas). Resultado: **`Vz(zona C) = 88 Bs/m²`**, consistente con la mediana declarada de 84. **Solo cubre zona C** (las demás zonas requieren más encuestas con distrito recuperado — R1).
- **`Ff` se deriva de la compacidad de Polsby-Popper** del polígono (mediana 0,61; rango 0,14–0,91), sin subjetividad.

### D7 — Anclaje referencial de construcción · **RATIFICADA (por delegación)**
**Ratificación de Saul:** "el anclaje a tu mejor criterio". Decisión: **ancla referencial única de `374 Bs/m²`** (mediana de 147 registros de encuesta utilizables), **sin desglose por tipología** y **no tributable / no aprobada**. Es un valor de exhibición para el demo, no una base imponible; la base imponible de construcción queda bloqueada hasta R4 (ordenanza).

### D8 — Tolerancia del demo y área de registro · **RATIFICADA**
**Ratificación de Saul:** "para esta demostración haz que las manzanas entren dentro de las tolerancias". Decisión: tolerancia de superficie **±15 m² aplicada solo a los 59 registros ALTA**; los casos que divergen se **exhiben como feature de QC** (no se ocultan). El **área GIS es el área de registro** (la declarada que diverge > tolerancia se marca, no se impone).

### D9 — Colector de campo con geo-captura GPS · **RECONSTRUIDA (numeración a verificar)**
El colector futuro debe nacer con, en orden de apalancamiento:
1. **Geo-captura GPS** del predio → **join espacial automático** al polígono distrito/zona/manzana. Resuelve de raíz toda la clase de problemas del join textual, recupera la llave y rompe los empates de lotes homogéneos. **Es la mejora de mayor apalancamiento.**
2. **Llave compuesta validada en tiempo real** contra la capa de predios (distrito obligatorio).
3. **Validación de coherencia de montos y superficies en captura** (antes de guardar).
4. **Foto con ruta almacenada** (objeto en MinIO), geo-etiquetada y vinculada a predio + encuesta + medición — no un flag SI/NO. Modelo recomendado: tabla `evidencia (uca_id, tipo, ruta_objeto, hash, captura)`.

---

## Reglas de QC / validación en captura

Cada regla está atada a un error real contado en las 198 encuestas.

| Problema observado | Regla del colector |
|---|---|
| Texto libre donde hay catálogo (81 "REVISAR_IMAGEN" en tipo de edificación) | **Desplegables** obligatorios; cero texto libre donde hay catálogo. |
| Total ≠ terreno + construcción (42/198 = 21%) | **Validación aritmética en captura**: avisa antes de guardar. |
| Superficie declarada disparatada (Reg.50: 677 vs ~400 GIS) | Al elegir el predio, muestra la **superficie GIS**; si la declarada diverge > tolerancia, **alerta en el momento**. |
| `Predio="00/ilegible"`, sup=52.000 (Reg.196) | **Código de predio validado contra la capa** (debe existir); numéricos con **rango**; obligatorios. (`52.000` es casi seguro separador de miles o monto contaminando el campo.) |
| Superficie nula (Reg.110, 178) | Campos obligatorios; no guarda sin ellos. |
| Fechas con frases enteras dentro del campo | Tipo **fecha** estricto. |
| Llaves duplicadas (20) | Llave compuesta validada en tiempo real (D9). |
| Foto solo flag, sin ruta | **Foto obligatoria** con ruta/objeto (MinIO), geo-etiquetada (D9). |

---

## Hallazgos de calidad en los `.dbf` (para Sprint 4/5)

- `PRE_NO_FOT` (2.352) es **subconjunto** de `PRE_SIS_UYU` (11.985), no predios aparte (intersección 2.352/2.352). **No sumar.**
- **17 edificaciones huérfanas** (triplete sin predio).
- `EDIFICACION.cod_geo` **sucio**: 8 valores, incluye malformados (`"4-12-05-01"`, `"01-0001-022"`, `"05-11-06-02"`) — probables registros de otros municipios o errores.
- `cod_man` **no es único global** (189) → llevar siempre distrito (685 combinaciones). Discrepancia menor: 685 combinaciones en predios vs 684 filas en `MAN_SIS_UYU` (1 manzana sin fila).
- Columnas 100% vacías en predio: `verificado`, `fecha de v`, `edi_suptot`, `cuv`, `cma`, `cpr`, `dir_urb`. `nompro` 79% vacío.
- Vía sin clave hacia predio: relación solo espacial o por nombre (frágil).

---

## Requerimientos / bloqueos (R1–R5)

| # | Requerimiento | Por qué | Impacto si falta |
|---|---|---|---|
| **R1** | **Recuperar el DISTRITO** de las 198 encuestas (re-transcribir del formulario; el PDF lo tiene, el CSV lo perdió) + completar las 59 MANUAL | El join encuesta↔predio↔zona depende de él | Sin esto, ~97% de encuestas no conectan y no hay `Vz` fuera de zona C |
| **R2** | Confirmar significado/unidad de `predio.val_zon` | Era el supuesto puente zona→valor | Resuelto por D5 (descartado); R2 cerrado |
| **R3** | ¿Existe **año de construcción** y **estado de conservación** en algún insumo? | `Fa` y `Fe` de construcción | Sin ellos, `Vc` no se calcula (captura de campo futura) |
| **R4** | ¿Caranavi/Uyuni tienen **estudio de valor Bs/m² por tipología** + ordenanza? | `Tip`; la norma prohíbe inventarlo | `Vc` queda como dependencia externa hasta ordenanza |
| **R5** | ¿Los SHP traen **geometría** (no solo atributos)? | `Ff`, frente/fondo y join espacial de rescate | Resuelto: hay geometría (EPSG:32719); R5 cerrado |

**Tareas humanas que destraban la Fase A:** R1 (re-transcribir distrito + completar las 59 de `encuestas_distrito_PENDIENTE.csv`). **Fase B:** R4 (ordenanza de valores por tipología).

---

## Consecuencias

- **Se puede valuar terreno (Fase A) en zona C hoy**, una vez resuelto R1, con `Vz=88` y la fórmula `Vt`.
- **La construcción (Fase B) queda explícitamente bloqueada** por R3/R4 — esto es rigor normativo, no carencia del sistema: la norma prohíbe inventar `Tip`, `Fa`, `Fe`.
- El **colector con GPS (D9)** es la inversión de diseño que evita que los 198 problemas de hoy se repitan mañana.
- La **llave de negocio** del sistema debe ser el triplete con distrito (D2/D3), no códigos administrativos como `codigo_origen`.

---

## Anexo — evidencia cuantitativa de la sesión

- Predios 11.985 (clave única `cod_uv+cod_man+cod_pred`); encuestas 198.
- Join espacial: 0 predios sin zona, 0 en >1 zona; ground-truth `3-2-24 → zona C` ✓.
- Resolución encuestas: ALTA 59 / MEDIA 80 / MANUAL 59. dif sup ALTA: med 0,12 · p90 10,5 · máx 32,4 m². MANUAL: 37 sin match de superficie, 21 empates < 0,5 m², 2 sin superficie, 1 código de predio ilegible.
- `val_zon`: 18 valores, moda 10, rango 5–22, 117 ceros; correlación monótona con material de vía.
- Compacidad Polsby-Popper: med 0,61 (rango 0,14–0,91).
- Coherencia montos encuesta: 42/198 (21%) no cuadran.
- Construcción: ancla referencial 374 Bs/m² = mediana de 147 registros utilizables.
- Artefactos generados por Claude Code (en `...DATOS`, fuera del repo): `crosswalk_encuesta_predio.csv` (198), `encuestas_distrito_PENDIENTE.csv` (59), `analisis_join_espacial.txt`.

---

## Pendiente de cierre (post-expo)

1. Reconciliar la numeración de **D2, D3, D4, D6, D9** contra el `.md` original si reaparece.
2. Confirmar que **0044** es el número más alto de la serie (si no, renumerar este ADR).
3. Evaluar **partir** este ADR consolidado en tres durables — *llave/modelo de datos* (D1–D3, D5), *motor de valuación* (D4, D6, D7) y *colector* (D9) — separando las decisiones de demo, time-bound (D8 y el uso-demo del ancla D7), que serán superseded tras la exposición.
4. Pasar a **Aceptada** una vez ratificadas D2, D3, D4, D6, D9.
