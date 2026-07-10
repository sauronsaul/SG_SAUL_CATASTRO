# DISEÑO — VERSIONADO DE DATASETS CATASTRALES (DatasetVersion)

**Documento de diseño técnico — insumo para Fase 1 (T-1.2 y T-1.3)**
**Versión:** 1.0 — 2026-07-10
**Autor:** Claude (planificador). **Aprobación:** Saul. **Ejecución:** Codex.
**Base:** ADR 0049, Plan Maestro §3 (D-02) y §5 (Fase 1). Evidencia de referencia: snapshot 2026-07-10 (develop `6fa88b5`, esquema actual de `dominio.predios` y agregado `Importacion` de Sprint 3).

---

## 1. Propósito

Especificar en detalle cómo se implementa el reemplazo completo versionado de las 7 capas SHP, de modo que Codex pueda ejecutarlo sin interpretar. Este documento resuelve el problema más delicado del diseño: **cómo reemplazar los datos importados en cada entrega municipal sin romper los datos que el sistema genera sobre ellos** (trámites, certificados, valuaciones, documentos, auditoría).

## 2. El problema central y la decisión que lo resuelve

El esquema actual tiene una tensión no resuelta: `dominio.predios` cumple hoy **doble función** — es a la vez el destino de la importación SHP y el ancla de datos generados por el sistema (`historial_estados`, `relaciones_predio_propietario`, `documentos` referencian `predio_id`). Un reemplazo ingenuo ("borrar predios e insertar los nuevos") huérfana o destruye esas relaciones. Un catastro no puede permitirse eso jamás.

**Decisión de diseño D-V1 — Separación entre capa importada y registro maestro:**

- **Capa importada (versionada, reemplazable):** los datos crudos de cada entrega SHP viven en tablas propias del módulo de importación, selladas con `dataset_version_id`. Cada importación crea un juego completo nuevo. Las versiones son inmutables una vez confirmadas.
- **Registro maestro (persistente, nunca se reemplaza):** `dominio.predios` deja de ser "el destino de la importación" y pasa a ser el **registro catastral persistente**. Cada predio maestro tiene un `id` estable de por vida, identificado por el triplete canónico `(cod_uv, cod_man, cod_pred)`. Todo dato generado por el sistema cuelga de ese `id` estable y sobrevive a cualquier reimportación.
- **Reconciliación:** el puente entre ambos mundos. Al activar una versión, un proceso determinista compara la capa de parcelas de la versión activa contra el registro maestro por triplete canónico, y aplica altas / actualizaciones / marcas de ausencia según las reglas del §6. Nunca borra.

Esta decisión refina el ADR 0049 sin contradecirlo: el "reemplazo completo" aplica a la capa importada; el registro maestro se *sincroniza*, no se reemplaza.

## 3. Modelo de datos

Esquema nuevo sugerido: `importacion` (o reutilizar `dominio` con prefijo si el patrón actual del repo lo prefiere — Codex decide conforme a la convención existente y lo reporta).

### 3.1 `importacion.dataset_versiones`

| Columna | Tipo | Notas |
|---|---|---|
| `id` | uuid PK | |
| `numero_version` | int | Correlativo por municipio, ≥ 1 |
| `municipio_codigo` | text | Preparación multi-tenant; en la instancia actual, constante (Uyuni) |
| `estado` | text | Ver máquina de estados §4 |
| `importacion_id` | uuid FK | Vincula al agregado `Importacion` existente que la produjo |
| `origen_descripcion` | text | Ej. "Entrega GAM Uyuni 2026-07, 7 capas SHP" |
| `creado_at/por`, `activado_at/por`, `archivado_at/por` | | Trazabilidad completa |

**Invariante I-1:** a lo sumo **una** versión `Activa` por `municipio_codigo`. Se refuerza con índice único parcial: `CREATE UNIQUE INDEX ... ON dataset_versiones (municipio_codigo) WHERE estado = 'Activa'`.

### 3.2 Tablas de capas: `importacion.capa_<nombre>` (una por capa, 7 en total)

Estructura común: `id` uuid PK, `dataset_version_id` uuid FK (NOT NULL, indexado), `geometria` (tipo PostGIS según capa, **SRID 32719 obligatorio** — constraint `ST_SRID(geometria) = 32719`), columnas de atributos según el perfil de importación de la capa, `atributos_extra` jsonb para columnas del SHP no mapeadas (nada se pierde), `fila_origen` int (número de feature en el SHP, para trazabilidad de errores).

Para la capa de parcelas, además: `cod_uv`, `cod_man`, `cod_pred` (los tres NOT NULL) y `superficie_sig` calculada (`ST_Area`).

**Punto abierto PA-1 (bloqueante para T-1.3, no para T-1.2):** los nombres y esquemas exactos de las 6 capas restantes deben confirmarse inspeccionando los SHP reales de Uyuni (`ogrinfo -so` sobre cada archivo). El primer prompt de Fase 1 debe incluir esa inspección como tarea de diagnóstico y sus resultados definen los perfiles de importación de las 6 capas nuevas.

### 3.3 `dominio.predios` (registro maestro) — cambios

Se agregan columnas, no se quita ninguna:

- `cod_uv`, `cod_man`, `cod_pred` (NOT NULL tras backfill) + **índice único** sobre el triplete. Backfill desde los datos ya importados (los 11,985 actuales). Si el backfill detecta duplicados de triplete en los datos existentes, la migración DEBE fallar ruidosamente y se resuelve con evidencia antes de continuar (según ADR 0045 el triplete es único; esto lo verifica en frío).
- `presente_en_version_activa` boolean NOT NULL default true.
- `ultima_version_vista_id` uuid FK a `dataset_versiones` (última versión donde el triplete apareció).
- `geometria` y atributos importados existentes se mantienen: son la **proyección** de la versión activa sobre el maestro, actualizada por la reconciliación.

**Invariante I-2:** los datos generados (`historial_estados`, `relaciones_predio_propietario`, `documentos`, y a futuro trámites/certificados/valuaciones) referencian SIEMPRE `predios.id`, nunca filas de `capa_*`. Las capas versionadas no tienen FKs entrantes desde datos generados.

## 4. Ciclo de vida de una versión (máquina de estados)

```
EnCarga → PreviewListo → Activa → Archivada
   ↓            ↓
Fallida     Descartada
```

- **EnCarga:** filas de capas insertándose (job asíncrono de T-1.1). Escritura permitida solo en este estado.
- **PreviewListo:** carga completa + validaciones ejecutadas; existe reporte de preview (§5). Inmutable desde aquí: guarda C# + modelo híbrido de trigger BD sobre `capa_*`. `BEFORE UPDATE OR DELETE FOR EACH ROW` consulta el estado de la versión por su PK: permite `UPDATE` solo en `EnCarga` y `DELETE` en `EnCarga`, `Fallida` o `Descartada`; no aplica a `INSERT` para no penalizar la carga masiva. `BEFORE TRUNCATE FOR EACH STATEMENT` rechaza incondicionalmente la operación; la purga excepcional usa `DELETE` fila a fila.
- **Activa:** única versión consultable por el sistema. La transición PreviewListo→Activa ejecuta la reconciliación (§6) **dentro de la misma transacción** (12k filas: volumen trivial, sin necesidad de lotes).
- **Archivada:** versión que fue activa y fue sucedida. Consultable para trazabilidad histórica; jamás se borra.
- **Fallida / Descartada:** carga con error o preview rechazado por el operador. Sus filas de capas pueden purgarse físicamente (única excepción a la inmutabilidad: datos que nunca fueron oficiales no son historia).

Guardas de transición en el dominio, con el mismo estilo de `RegistrarConteosPreview`/`RegistrarConteosConfirmacion` del agregado `Importacion` actual. La activación exige: estado origen `PreviewListo`, preview sin errores bloqueantes, y confirmación explícita del operador (rol autorizado).

## 5. Validaciones de preview (por versión, antes de poder activar)

Bloqueantes (impiden activar):
1. Capa de parcelas: tripletes duplicados dentro de la versión (`GROUP BY cod_uv, cod_man, cod_pred HAVING count(*)>1` debe devolver 0).
2. Capa de parcelas: tripletes con componentes nulos o vacíos.
3. Geometrías con SRID ≠ 32719 en cualquier capa.
4. Conteo de capas cargadas ≠ 7 (una versión es la entrega completa, por definición de D-02).

Observaciones (no bloquean; van al reporte y se devuelven al GAM como hallazgos, nunca se corrigen en silencio):
5. Geometrías inválidas (`ST_IsValid = false`) — se importan tal cual, marcadas; el reporte lista capa + `fila_origen` + `ST_IsValidReason`.
6. Diferencias de conteo relevantes contra la versión activa anterior (ej. −5% de parcelas) — señal de entrega incompleta.
7. Parcelas cuya geometría cambió de posición más allá de un umbral configurable respecto a la versión anterior (control de calidad, no bloqueo).

El reporte de preview se persiste (jsonb en `dataset_versiones` o tabla propia) — es evidencia y es entregable al GAM.

## 6. Reconciliación (capa parcelas → registro maestro)

Se ejecuta en la transacción de activación. Comparación por triplete canónico entre la capa de parcelas de la versión que se activa y `dominio.predios`. Cuatro casos, reglas cerradas:

**R-1 ALTA — triplete en la versión, no en el maestro:** se crea predio maestro nuevo (id nuevo, atributos y geometría desde la capa, `presente_en_version_activa=true`, `ultima_version_vista_id` = versión). Auditoría: acción `PREDIO_ALTA_POR_IMPORTACION`.

**R-2 ACTUALIZACIÓN — triplete en ambos:** se actualizan en el maestro los atributos de origen importado y la geometría con los valores de la nueva versión; `presente_en_version_activa=true`; `ultima_version_vista_id` actualizado. Los campos de gestión del sistema (estado del predio, `requiere_revision`, `detalle_revision` y todo dato generado) NO se tocan. Auditoría por predio modificado con valor anterior/nuevo (el interceptor existente ya lo hace). Si nada cambió (misma geometría y atributos), no se escribe nada — evita 12k updates vacíos y ruido de auditoría; la comparación de geometría usa `ST_Equals` o hash de WKB.

**R-3 AUSENCIA — triplete en el maestro, no en la versión:** el predio maestro NO se borra ni se soft-deletea. Se marca `presente_en_version_activa=false` y `requiere_revision=true` con `detalle_revision` explicando "ausente en dataset versión N". Sus certificados, trámites y documentos históricos permanecen intactos y válidos como actos administrativos ya emitidos. Un predio ausente no puede recibir NUEVOS certificados ni trámites mientras esté en ese estado (regla de negocio a implementar en Fases 4-5; aquí solo se deja la marca). Auditoría: `PREDIO_AUSENTE_EN_VERSION`.

**R-4 CONFLICTO estructural:** no aplica en runtime — los duplicados dentro de la versión son bloqueante de preview (§5.1) y los duplicados en el maestro son imposibles por el índice único. Si la reconciliación encontrara un estado imposible, aborta la transacción completa (la versión queda en PreviewListo, el sistema queda como estaba).

La reconciliación produce un **resumen persistido**: conteos de altas/actualizadas/sin cambio/ausencias, adjunto a la versión. Ese resumen es la evidencia de cierre del ciclo.

## 7. Activación, rollback y consulta

- **Activación atómica:** una transacción que (a) valida guardas, (b) marca la Activa anterior como Archivada, (c) marca la nueva como Activa, (d) ejecuta reconciliación, (e) persiste el resumen. Todo o nada; el índice único parcial I-1 hace de cinturón de seguridad ante concurrencia.
- **Rollback = reactivación:** activar una versión Archivada es legal y usa el mismo camino (misma transacción, misma reconciliación, que es determinista e idempotente respecto del estado destino). No existe "deshacer" mágico: volver atrás es reconciliar contra la versión anterior, con su propia auditoría. Esto convierte el rollback en operación de primera clase, no en cirugía.
- **Consulta:** el sistema (visor, certificados, valuación) lee SIEMPRE del registro maestro + capas de la versión Activa (las 6 capas no-parcelas se sirven directamente filtradas por la versión activa; una vista SQL `capa_<nombre>_activa` por capa simplifica los consumidores). Las versiones Archivadas solo se consultan explícitamente para trazabilidad.

## 8. Casos límite resueltos (para que no queden colgando)

1. **Predio ausente con certificado emitido:** cubierto por R-3 — el certificado es un acto administrativo histórico, permanece; el predio queda en revisión y bloqueado para actos nuevos.
2. **Triplete reaparece después de estar ausente:** R-2 lo reactiva (`presente_en_version_activa=true`); `requiere_revision` NO se limpia automáticamente — lo limpia un humano, porque la desaparición/reaparición amerita ojos.
3. **Importación fallida a mitad:** la versión queda `Fallida`, sus filas se purgan, nada tocó el maestro (la reconciliación solo ocurre en activación). Repetir es crear una versión nueva — idempotencia por diseño.
4. **Cambio de codificación del GAM (renumeración de tripletes):** fuera del alcance de la reconciliación automática — un cambio masivo de tripletes aparecería como muchas altas + muchas ausencias, y la observación §5.6 lo delataría en preview. Se maneja como decisión con el GAM, no como magia del sistema. Registrado como riesgo en el reporte de preview si altas+ausencias > umbral configurable (default sugerido: 10%).
5. **Dos operadores activando a la vez:** el índice único parcial + transacción serializable en la activación lo resuelve; el segundo falla limpio.

## 9. Impacto en el código existente

- Agregado `Importacion` (Sprint 3): se extiende — una `Importacion` confirmada produce/alimenta una `DatasetVersion`. Evaluar en diagnóstico si conviene que `DatasetVersion` sea agregado propio con `importacion_id` de referencia (recomendado: sí, responsabilidades distintas).
- `perfiles_importacion` / `mapeos_columna` existentes: se reutilizan; faltan los perfiles de las 6 capas nuevas (dependen de PA-1).
- Interceptor de auditoría y trigger ADR 0044: sin cambios; la reconciliación pasa por `SaveChanges` normal y se audita sola.
- Seeders: corregir de paso la inconsistencia detectada en el snapshot ("usos de suelo para Caranavi" en instancia Uyuni) — tarea menor, mismo sprint.
- Migraciones nuevas: sí, esta fase las requiere (tablas nuevas + columnas en predios + índices + trigger). Primera prueba real del guard `SG_APPLY_MIGRATIONS`.

## 10. Orden de implementación sugerido para Fase 1 (prompts)

1. **Prompt #002 — Diagnóstico de datos:** `ogrinfo` de las 7 SHP reales (resuelve PA-1), verificación de unicidad de tripletes en los datos actuales de `dominio.predios` (pre-backfill), y propuesta de perfiles para las 6 capas. Solo lectura, sin cambios.
2. **Prompt #003 — Modelo y migraciones:** DatasetVersion + capas + columnas maestro + backfill + índices + trigger de inmutabilidad + tests de dominio.
3. **Prompt #004 — Importación asíncrona (T-1.1):** endpoint 202+polling + job en background, cargando hacia versiones EnCarga.
4. **Prompt #005 — Preview + activación + reconciliación:** validaciones §5, transición de estados, reconciliación §6, resumen persistido, tests E2E.
5. **Prompt #006 — Importación real de Uyuni v1** (7 capas) con evidencias de cierre de fase.

## 11. Criterios de aceptación verificables (para el cierre de Fase 1)

1. `SELECT count(*) FROM dataset_versiones WHERE estado='Activa'` = 1.
2. Unicidad de triplete en maestro y en capa de parcelas de la versión activa: consultas `HAVING count(*)>1` devuelven 0 filas.
3. Intento de UPDATE sobre una fila de capa de versión Activa → rechazado por trigger (evidencia de error, como el test TAMPERED de auditoría).
4. Reconciliación de una segunda importación de prueba produce el resumen esperado en un caso sintético controlado (1 alta, 1 actualización, 1 ausencia) verificado por SQL.
5. Predio marcado ausente conserva sus `documentos`/`historial_estados` (SQL join demostrándolo).
6. 7 capas de Uyuni cargadas como versión 1 con conteos por capa reportados y 11,985 parcelas reconciliadas (esperado: ~11,985 actualizaciones/sin-cambio, 0 o pocas altas, ausencias justificadas si las hay).
7. Suite completa de tests en verde, nativo y CI.

---

*Cambios a este diseño se registran aquí con fecha y motivo, y si alteran una decisión, con ADR nuevo. Punto abierto vigente: PA-1 (esquemas de las 6 capas — se cierra con el Prompt #002).*

---

**Registro de cambios**

- **2026-07-10:** §4 actualizado al modelo híbrido row/statement implementado. Motivo: la autorización T1-T4 precisó que `UPDATE` y `DELETE` requieren inspeccionar `OLD.dataset_version_id` por fila, mientras que `TRUNCATE` se bloquea incondicionalmente por sentencia.
