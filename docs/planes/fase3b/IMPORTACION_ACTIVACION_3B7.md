# Fase 3.B.7 — Importación de la capa predial de Caranavi (PRE_NOF_CAR)

**Fecha:** 28 de julio de 2026
**Estado:** COMPLETADA con alcance delimitado por ADR 0064.
**Versión resultante:** `dominio.dataset_versiones` v3 de `022001`, id
`5a6dbb05-41d5-4363-815d-fa963223b792`, estado Activa.

---

## 1. Resultado

La capa `PRE_NOF_CAR` (6.573 predios) está cargada, activa y visible en el visor
institucional de Caranavi. Es la primera cartografía predial del municipio dentro
del sistema.

Conteos verificados en la versión activa:

| Capa | Filas |
|---|---|
| `capa_manzanas` | 637 |
| `capa_areas_urbanas` | 17 |
| `capa_puntos_geodesicos` | 33 |
| `capa_predios_no_fotografiados` | 6.573 |

`dominio.predios` sigue **sin registros de `022001`** (solo Uyuni, 11.985 filas).
Es el comportamiento correcto y esperado: el esquema municipal de Caranavi no
declara `TipoCapa.Predios`, por lo que la activación omitió la reconciliación y
lo declaró expresamente en `resumen_reconciliacion`:

```
{"altas": 0, "omitida": true, "ausencias": 0, "sinCambio": 0,
 "actualizadas": 0, "motivoOmision": "Esquema municipal sin capa de predios."}
```

## 2. El hallazgo previo era erróneo

La sesión comenzó con un archivo staged, `HALLAZGO_PIPELINE_3B7.md`, que atribuía
el bloqueo a la falta de un parámetro `perfil_codigo` en el endpoint. **Ese
diagnóstico era falso en tres puntos**, verificados contra código:

1. `InspectorPaqueteVersionado` sí evalúa `Obligatoria`
   (línea `if (!definicion.Obligatoria && encontrados.Count == 0) continue;`).
   Una capa opcional ausente no invalida el paquete.
2. El perfil `caranavi-versionado-predios-no-fotografiados` sí tiene consumidor:
   forma parte del esquema municipal que resuelven el handler, el worker y la
   activación.
3. La activación **no** es aditiva: archiva la versión activa anterior y no copia
   filas hacia adelante.

El rechazo original del ZIP de una sola capa se produjo porque faltaban las tres
capas **obligatorias**, no por la presencia de una cuarta. El cambio de backend
propuesto en aquel documento era innecesario. El archivo se descartó sin
commitear.

## 3. Solución aplicada

Paquete completo de cuatro capas, conforme a ADR 0063 (snapshot municipal
completo). Ensamblado a partir del contenido de `caranavi_v1.zip` —el mismo
paquete que originó la v2 activa— más los archivos de `PRE_NOF_CAR`:

```
caranavi_v3.zip — 17 archivos, todos en la raíz del ZIP
  AREA_URBANA.{shp,dbf,shx,prj}
  MANZANOS_PROY.{shp,dbf,shx,prj}
  puntos_geodesicos.{shp,dbf,shx,prj}
  PRE_NOF_CAR.{shp,dbf,shx,prj,cpg}
```

Ningún cambio de backend. Cero migraciones.

## 4. Preview: sin bloqueantes

`TieneBloqueantes: false`, `Bloqueantes: []`, `Observaciones: []`.

`DiferenciasContraActiva` confirmó que las capas base se reprodujeron sin
alteración respecto de la v2:

| Capa | Activa (v2) | Versión (v3) | Diferencia |
|---|---|---|---|
| `capa_areas_urbanas` | 17 | 17 | 0 |
| `capa_manzanas` | 637 | 637 | 0 |
| `capa_puntos_geodesicos` | 33 | 33 | 0 |
| `capa_predios_no_fotografiados` | 0 | 6.573 | +6.573 |

Se registraron 78 geometrías inválidas con código **O1 (observación, no
bloqueante)**, todas de tipo self-intersection y todas en `capa_manzanas`. Son
las mismas 78 ya presentes en la v2, procedentes del mismo archivo fuente
`MANZANOS_PROY.shp`. No es una regresión y se trata conforme a ADR 0052.

## 5. Calidad de los datos importados

**Geometría: impecable.**

| Métrica | Valor |
|---|---|
| Geometrías nulas | 0 |
| Geometrías inválidas | 0 |
| SRID mínimo / máximo | 32719 / 32719 |

Notablemente mejor que la importación de Uyuni, que arrastró 78 filas inválidas.

**Clave catastral: incompleta, como se preveía.**

El `.dbf` de origen declara **solo dos campos**, confirmado por `ogrinfo`:

```
cod_pred: Integer (9.0)
cod_geo:  String (80.0)
Feature Count: 6573
```

`cod_uv` y `cod_man` **no existen en el archivo**. El perfil sembrado los mapea,
pero al no hallar la columna de origen el mapeador asigna null sin error, porque
el carril `PrediosNoFotografiados` usa `AEnteroOpcional`. Resultado en base:

| Columna | No nulos |
|---|---|
| `cod_uv` | 0 |
| `cod_man` | 0 |
| `cod_pred` | 4.486 |
| `codigo_geografico` | 6.573 |

`codigo_geografico` contiene el valor constante `022001` —el código INE del
municipio— en todas las filas. No aporta identificación por predio.

## 6. Naturaleza de `cod_pred`: número de lote, no identificador

Hallazgo relevante para el cierre de fase. Distribución de valores:

| `cod_pred` | Filas |
|---|---|
| null | 2.087 |
| 1 | 478 |
| 4 | 417 |
| 2 | 410 |
| 3 | 401 |
| 5 | 350 |
| 6 | 316 |
| 7 | 287 |
| 8 | 278 |
| 9 | 182 |
| 10 | 169 |
| 11 | 116 |

Rango 0–952 con **solo 96 valores distintos** sobre 4.486 filas pobladas. La
distribución decreciente casi monótona desde el lote 1 identifica el campo como
**número de lote dentro de manzana**, no como identificador de predio.

Esto reinterpreta una cifra anterior: la "colisión máxima de 478" registrada en
3.B.4 no era una anomalía, sino simplemente el número de predios cuyo lote es 1.
Con 637 manzanas, esa repetición es lo esperado.

**Consecuencia:** la cobertura del 68,2 % de `cod_pred` es una métrica mucho más
débil de lo que su nombre sugiere. Sin `cod_man` que lo ancle, un número de lote
no discrimina nada. Obtener el registro alfanumérico con asignación de manzana
del GAM Caranavi no es un paso más hacia la promoción a `TipoCapa.Predios`: es el
paso determinante.

## 7. Incidente de entorno: contenedores con código obsoleto

Durante la sesión se detectó que **el contenedor `sg_api` ejecutaba una imagen
huérfana** (`sha256:94ffe232…`, ya ausente del almacén local de Docker) anterior
al commit `36fe8e4`. Síntoma: `POST /api/importaciones/versiones/{id}/descartar`
devolvía 404 pese a existir en `ImportacionesController.cs:117`.

El tag `sg-catastro-api:latest` apuntaba a otra imagen distinta de la que corría
el contenedor. Causa: `scripts/start-local.sh` invoca
`docker compose up -d --remove-orphans` **sin `--build` ni `--force-recreate`**,
por lo que un contenedor existente sobrevive intacto aunque la imagen se
reconstruya.

`sg_web` presentaba el mismo problema por otra vía: su imagen se construyó ocho
minutos **antes** del commit `407a70b`, que habilita la configuración
multimunicipio del visor.

Ambos servicios se reconstruyeron desde HEAD con `--force-recreate`. La deriva
no incluía ninguna migración, por lo que el esquema de base no se vio afectado.
Verificación posterior: `descartar` pasó de 404 a 401 sin token, y el selector de
municipio apareció en el visor.

**Deuda abierta:** `start-local.sh` debe detectar o prevenir esta situación.

## 8. Higiene ejecutada

- Descartado el archivo staged erróneo `HALLAZGO_PIPELINE_3B7.md` (nunca
  commiteado).
- Descartada la versión huérfana v1 de Caranavi
  (`e1d02c6a-c1a2-494c-8903-911dfcaf0929`), que llevaba desde su carga en estado
  `PreviewListo` reteniendo 687 filas `capa_*` sin propósito. La purga eliminó
  esas 687 filas y dejó intactas las 637 / 17 / 33 de la v2.
- Corregido `scripts/importar.ps1`, que era anterior a ADR 0060 y no enviaba el
  campo multipart `municipio_codigo`. Se añadió `-MunicipioCodigo` con validación
  de formato INE, la acción `descartar`, y `-Base` parametrizable.

## 9. Estado final de versiones de Caranavi

| Versión | Estado |
|---|---|
| v1 | Descartada |
| v2 | Archivada |
| v3 | Activa |

La v2 es recuperable en cualquier momento mediante `ReactivarDesdeArchivada`
(activar una versión en estado Archivada). La operación es reversible.

## 10. Verificación en el visor

Configuración devuelta por `GET /api/visor/022001/configuracion`:
`numeroVersionActiva: 3`, cuatro capas declaradas incluyendo
`predios-no-fotografiados` (orden 60, minZoom 16, color `#7C3AED`), y
`capacidades.tienePredios: false`.

El bbox municipal pasó a abarcar aproximadamente 22 km este-oeste, coherente con
la extensión de `PRE_NOF_CAR` medida por `ogrinfo`
(641.933–663.806 E). `ExtensionMunicipalService` ya incorpora la geometría de la
capa nueva.

Confirmado visualmente: el visor renderiza los predios como polígonos morados
subdividiendo las manzanas, con etiquetas de puntos geodésicos, y el selector de
municipio ofrece Caranavi.

## 11. Observación de producto

El panel del visor muestra el mensaje *"Este municipio aún no tiene catastro
predial cargado"* mientras la pantalla renderiza miles de predios. Técnicamente
es correcto —`tienePredios: false`, no hay maestro predial— pero ante un usuario
del GAM la contradicción aparente puede leerse como un error del sistema.

Se sugiere reformular el texto para distinguir cartografía cargada de registro
predial consultable. No se modifica en esta fase.

## 12. Qué queda pendiente

- Registro alfanumérico con asignación de manzana del GAM Caranavi, o
  componente de campo, para reconstruir `cod_man`.
- Promoción a `TipoCapa.Predios` conforme a las condiciones de ADR 0064.
- Corrección de `scripts/start-local.sh` para prevenir deriva de contenedores.
- Reconstrucción del índice de ADRs en `docs/decisiones/README.md`, desatendido
  desde el 0052.
