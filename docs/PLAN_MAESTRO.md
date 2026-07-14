# PLAN MAESTRO — SG_SAUL_CATASTRO
## De la base actual al mejor sistema catastral municipal de Bolivia

**Versión:** 1.1
**Fecha:** 12 de julio de 2026
**Autor del plan:** Claude (planificador/revisor del triángulo)
**Aprobación:** Saul (mediador/orquestador)
**Ejecutor:** ChatGPT Codex (con acceso a repositorio y terminal)

---

## 0. Propósito de este documento

Este es el documento rector del proyecto. Define: el protocolo operativo del triángulo Claude–Saul–Codex, el estado verificado del sistema, las decisiones técnicas resueltas, la visión del producto final, el roadmap completo por fases con criterios de cierre basados en evidencia, los riesgos, y la siguiente acción inmediata. Nada queda al azar: lo que no se puede decidir hoy queda registrado como **decisión pendiente con disparador explícito**, no como cabo suelto.

Regla de oro heredada del proyecto: **"debería funcionar" no cierra nada. Solo la evidencia cruda (código, logs, salida SQL, conteo de tests) cierra un ítem.**

---

## 1. Protocolo operativo del triángulo

### 1.1 Roles

| Rol | Responsable | Función |
|---|---|---|
| Planificador/Revisor | Claude | Analiza evidencia, diseña, redacta prompts para Codex, revisa salidas de Codex, mantiene ADRs y este plan |
| Mediador/Orquestador | Saul | Transporta prompts y evidencia entre Claude y Codex; revisa ambas direcciones; toma decisiones finales |
| Ejecutor | ChatGPT Codex | Implementa exactamente lo especificado; devuelve evidencia cruda, nunca solo resúmenes |

### 1.2 Cómo enviar snapshots a Claude (respuesta a tu pregunta 1)

Copia la salida cruda de la terminal y pégala en el chat dentro de bloques de código (tres backticks ``` antes y después). Texto plano siempre, no capturas de pantalla, porque el texto se puede citar literalmente dentro de los prompts que redacto para Codex. Formato del snapshot de inicio de ciclo:

```
=== SNAPSHOT [fecha] ===
$ git log --oneline -10 develop
(salida)

$ git status
(salida)

$ docker ps -a --filter "name=sg_"
(salida)

$ [comando del último ítem completado, ej: dotnet test --logger "console;verbosity=minimal"]
(salida — al menos las últimas 20 líneas con el conteo final)
```

Si un comando expone secretos (`.env`, connection strings con password), **redacta el valor con `****` antes de pegar**. Esta regla ya es ley del proyecto y se mantiene.

### 1.3 Formato de los prompts que Claude redacta para Codex

Todo prompt que yo entregue tendrá esta estructura fija, para que Codex no improvise:

1. **CONTEXTO** — estado relevante del repo, rama, convenciones (Clean Architecture, MediatR, clave canónica).
2. **OBJETIVO** — qué se construye, en una frase.
3. **TAREAS** — pasos numerados, con rutas de archivos explícitas.
4. **RESTRICCIONES** — lo que NO debe hacer (nunca `git add .`, staging por archivo, comando de commit obligatorio sin trailers, no tocar `.env` sin cláusula de no-exposición, no modificar el trigger `trg_auditoria_immutable`, etc.).
5. **EVIDENCIA REQUERIDA** — comandos exactos cuya salida cruda Codex debe devolver (tests, SQL vía `information_schema`, logs).
6. **CRITERIO DE CIERRE** — condición binaria verificable.

### 1.4 Ciclo estándar de trabajo

1. Saul envía snapshot → 2. Claude valida estado y redacta prompt → 3. Saul revisa y lo pasa a Codex → 4. Codex ejecuta y devuelve evidencia → 5. Saul revisa y trae la evidencia a Claude → 6. Claude verifica contra el criterio de cierre → 7. Se cierra el ítem o se redacta prompt de corrección. Nunca se avanza al ítem siguiente con el anterior abierto.

### 1.5 Economía de tokens (tu punto 5)

Para reducir consumo en ambas IAs: (a) cada prompt a Codex es autocontenido — Codex no necesita historial; (b) los ítems se agrupan en lotes coherentes de 2–4 tareas cuando comparten contexto; (c) la evidencia que me traes es la mínima suficiente (conteo final de tests, no el log completo salvo fallo); (d) este documento maestro vive en el repo (`docs/PLAN_MAESTRO.md`) para que ninguna de las dos IAs tenga que reconstruir el contexto desde cero en sesiones nuevas.

---

## 2. Estado actual verificado (línea base)

Lo siguiente está cerrado con evidencia en ciclos anteriores:

**Arquitectura y stack.** .NET 10, EF Core 10, PostgreSQL 16 + PostGIS, Clean Architecture con MediatR (ADR 0026), MinIO, Caddy, Blazor WebAssembly (ADR 0046). Entorno canónico: BD `sg_catastro` en contenedor `sg_postgres` (localhost:5434), distinto de `sdga_postgres` (15435).

**Calidad y CI.** 174/174 tests pasando, incluyendo E2E contra PostGIS real. Job de regresión activo en CI (rama `ci/activate-test-job` mergeada a `develop`, commit `6fa88b5`). Sistema de detección de secretos en tres capas: wrapper de commit, hook pre-push, gitleaks en CI. CI totalmente verde.

**Dominio.** Módulo de importación SHP cerrado en Sprint 3 (tag `v0.3.0-importacion-shapefile`): agregado `Importacion` con handlers separados de preview/confirm y guardas de estado. Clave canónica única del predio: **triplete `(cod_uv, cod_man, cod_pred)`** (ADR 0045) — `cod_uv` = distrito; `(zona, manzana, predio)` es ambiguo con duplicados. 11,985 predios de Uyuni cargados (1 de 7 capas SHP). Identificador humano real: `zona+manzana+lote` (`codigo_origen` es constante municipal, no ID de parcela). Auditoría append-only con doble refuerzo: guarda C# + trigger PostgreSQL `trg_auditoria_immutable` a nivel de sentencia (ADR 0044).

**Valuación.** Fase A computable: valor de terreno Vz(zona C) = 88 Bs/m². Fase B (construcciones) **bloqueada** por falta de ordenanza municipal de valores por tipología.

**Normativa.** Cuatro documentos regulatorios bolivianos analizados (Instructivo, Guía Nacional 2024, Guía de Zonificación 2024, Reglamento Nacional de Catastro). Conclusión: el sistema debe incluir el módulo de valuación que la norma exige.

**Comercial.** Exposición municipal exitosa (19 junio), fuerte interés en Uyuni y Caranavi, pero **quieren ver producto, no palabras**. Caranavi solo tiene datos en formato CAD — requiere pipeline de conversión y planificación de levantamiento.

**Pendiente de verificación con snapshot:** estado real de `develop` hoy, estado del contenedor `sg_api` (último dato: `Exited (0)`, sin connection string para desarrollo nativo). **La línea base de este plan se confirma con el primer snapshot.**

---

## 3. Decisiones resueltas en este ciclo

### D-01 — PASO 1: entorno de ejecución para la importación asíncrona

**Decisión: modelo híbrido — desarrollo nativo contra el PostGIS containerizado; validación de paridad en contenedor antes de cada merge.**

Concretamente: la API corre nativa (`dotnet run` / `dotnet watch`) apuntando a `sg_postgres:5434`, con la connection string en `appsettings.Development.json` **usando variables de entorno o user-secrets para la contraseña, nunca hardcodeada**. El contenedor `sg_api` se revive y se usa solo como puerta de validación: antes de mergear a `develop`, la feature debe pasar el smoke test con `docker compose up` completo.

**Justificación:** eres un solo desarrollador y el flujo async 202+polling requerirá muchas iteraciones cortas (depuración con breakpoints, hot reload). Reconstruir la imagen Docker en cada iteración quema tiempo y tokens de Codex. Pero desplegaremos en contenedores (ADR 0047), así que la paridad se garantiza con la validación containerizada como criterio de cierre, no como entorno de trabajo diario.

**Implica dos tareas nuevas en Fase 0:** (T-0.1) configurar la connection string nativa de forma segura; (T-0.2) revivir `sg_api` y documentar el smoke test de paridad.

### D-02 — PASO 2: estrategia de carga de las 7 capas SHP

**Decisión: reemplazo completo versionado — cada importación es un snapshot completo de las 7 capas, con número de versión de dataset.**

Concretamente: el agregado `Importacion` evoluciona a manejar un `DatasetVersion`. Importar crea la versión N+1 completa; la versión N no se muta ni se borra (coherente con el invariante append-only y con el trigger de auditoría). Las consultas operan siempre sobre la versión activa. Una versión anterior puede consultarse para trazabilidad histórica.

**Justificación:** (a) la lógica incremental (diff capa por capa, detección de altas/bajas/modificaciones geométricas) es compleja y propensa a estados parciales corruptos — el peor enemigo de un catastro es un dato a medias; (b) la fuente de verdad real es la entrega SHP del municipio, que llega como paquete completo; (c) 11,985 predios × 7 capas es un volumen trivial para PostgreSQL — el costo de almacenamiento de versionar es despreciable; (d) el mismo pipeline sirve tal cual para Caranavi y para cualquier municipio futuro; (e) es idempotente: una importación fallida se descarta y se repite sin cirugía de datos.

**Matiz importante:** el reemplazo versionado aplica a los datos *importados* (la base gráfica y alfanumérica de origen municipal). Los datos *generados por el sistema* (trámites, certificados emitidos, valuaciones, auditoría) viven en tablas propias vinculadas por la clave canónica y **nunca** se reemplazan. La vinculación entre versiones se hace por el triplete `(cod_uv, cod_man, cod_pred)`.

---

## 4. Visión del producto final

"El mejor sistema catastral municipal de Bolivia" se traduce en algo medible: **un sistema que cubre el ciclo catastral completo que exige el Reglamento Nacional de Catastro, operable por un GAM pequeño o mediano sin personal técnico especializado, con trazabilidad total (auditoría inmutable), y desplegable por municipio en días, no meses.** Ningún competidor en el segmento de GAMs pequeños/medianos ofrece eso hoy.

### Módulos del producto final

**M1 — Núcleo catastral.** Base gráfica y alfanumérica completa (7 capas), importación asíncrona versionada, clave canónica, consulta e historial por predio. *(Base ya construida; se completa en Fase 1.)*

**M2 — Visor geográfico institucional.** Mapa dinámico servido desde PostGIS (no la demo estática Leaflet): búsqueda por `zona+manzana+lote` y por triplete canónico, capas conmutables, ficha de predio al clic, impresión de croquis. Herramienta diaria del técnico municipal.

**M3 — Valuación.** Fase A (terreno por zona homogénea) operativa; Fase B (construcciones por tipología) con motor paramétrico listo, alimentado por tabla de valores que el GAM aprueba por ordenanza. El sistema no espera la ordenanza para existir: se construye con tabla parametrizable y datos de prueba, y el día que la ordenanza sale, se cargan los valores oficiales y Fase B se activa. **Esto convierte el bloqueo político en un dato de configuración.**

**M4 — Certificados catastrales.** Emisión de certificado catastral oficial en PDF: datos técnicos del predio, croquis, valuación vigente, folio correlativo por gestión, código QR de verificación pública, firma del responsable municipal. Registro inmutable de cada emisión.

**M5 — Trámites catastrales internos.** Flujo de trabajo para los trámites del día a día municipal: transferencia (cambio de titular), subdivisión, fusión, actualización de mejoras (construcciones), rectificación de datos. Cada trámite: estado (iniciado → en revisión → aprobado/rechazado), documentos de respaldo en MinIO, efecto sobre el predio solo al aprobar, todo auditado.

**M6 — Integración tributaria.** Cálculo de la base imponible del IPBI a partir de la valuación (Ley 843 y ordenanzas municipales), generación de la liquidación anual por predio, y exportación. **Decisión pendiente DP-01:** integración con RUAT (que muchos GAMs usan) vs. módulo de liquidación propio — depende de cada municipio; se investiga con el GAM de Uyuni en Fase 6. El diseño contempla ambos caminos: el cálculo de base imponible es común; solo cambia el destino (export a formato RUAT o liquidación propia).

**M7 — Portal ciudadano.** Consulta pública de predio (datos no sensibles), verificación de certificados por QR/código, estado de trámite en línea. Es la cara visible ante el ciudadano y un argumento de venta fuerte ante los alcaldes.

**Plataforma transversal.** Autenticación y roles (administrador GAM, técnico catastral, ventanilla, consulta), respaldos automatizados, monitoreo, despliegue por municipio (cloud si el GAM tiene presupuesto; hosting administrado por ti como servicio adicional en tu planilla de costos si no — ambos con la misma imagen Docker, ADR 0047).

---

## 5. Roadmap por fases

Cada fase tiene objetivo, entregables, y **criterio de cierre basado en evidencia**. Las fases son secuenciales para un solo desarrollador; donde algo puede adelantarse en paralelo, se indica. Sin fechas impuestas (tu punto 9): el ritmo lo marca el cierre con evidencia, no el calendario. El orden está diseñado para que **el valor visible aparezca temprano** (los GAMs quieren ver producto): tras la Fase 2 ya hay algo demostrable en vivo; tras la Fase 4 ya hay un producto vendible.

---

### FASE 0 — Fundación operativa *(sprint actual, primera semana de trabajo)*

**Objetivo:** dejar el entorno de trabajo del triángulo 100% operativo según D-01, sin deuda de configuración.

**Entregables:**
- T-0.1: Connection string para desarrollo nativo (`appsettings.Development.json` + user-secrets/variable de entorno para el password). Cláusula de no-exposición en el prompt.
- T-0.2: Contenedor `sg_api` revivido; `docker compose up` levanta el stack completo; smoke test de paridad documentado (`docs/SMOKE_TEST.md`): API responde, conecta a BD, endpoint de salud OK.
- T-0.3: Este plan maestro commiteado en `docs/PLAN_MAESTRO.md`.
- T-0.4: ADR 0048 registrando D-01 y ADR 0049 registrando D-02.

**Criterio de cierre:** salida cruda de (a) `dotnet run` nativo respondiendo al endpoint de salud contra `sg_postgres:5434`; (b) `docker compose up` con `sg_api` en estado `Up` y mismo endpoint respondiendo; (c) 174/174 tests verdes en ambos modos; (d) `git log` mostrando los commits de docs y ADRs.

**CERRADA — 2026-07-12.** Se verificaron el modelo híbrido de ejecución nativa/contenedor y el guard explícito de migraciones, manteniendo paridad contra PostgreSQL y MinIO. La infraestructura y sus decisiones quedaron integradas mediante el PR #2.

---

### FASE 1 — Núcleo de datos completo (PASO 1 + PASO 2)

**Objetivo:** importación asíncrona robusta y las 7 capas de Uyuni en producción versionada.

**Entregables:**
- T-1.1: Endpoint de importación asíncrono: `POST /importaciones` responde `202 Accepted` + `Location`; `GET /importaciones/{id}` para polling de estado (pendiente → procesando → preview_listo → confirmada/fallida). Job en background (BackgroundService o canal interno; evaluar Hangfire solo si se justifica — ADR si se adopta).
- T-1.2: Modelo `DatasetVersion` según D-02: importar crea versión completa; activación atómica de versión; consultas sobre versión activa.
- T-1.3: Soporte de las 7 capas SHP: definición de esquema por capa, validaciones geométricas (`ST_IsValid`/`ST_MakeValid` con reporte, no corrección silenciosa), reporte de preview por capa (conteos, geometrías inválidas, claves duplicadas).
- T-1.4: Importación real de las 7 capas de Uyuni como versión 1 oficial.
- T-1.5: Suite de tests extendida cubriendo async, versionado y las 7 capas (E2E contra PostGIS real, como es estándar del proyecto).

**Criterio de cierre:** (a) salida cruda de una importación completa de las 7 capas vía API: request 202, secuencia de polling, confirmación; (b) SQL vía `information_schema` + conteos por capa de la versión activa; (c) verificación de que los 11,985 predios mantienen el triplete canónico único (`SELECT ... GROUP BY ... HAVING count(*)>1` devolviendo 0 filas); (d) todos los tests verdes en CI.

**Riesgo específico:** calidad geométrica de las 6 capas nuevas — desconocida hasta el preview. Mitigación: el preview reporta antes de confirmar; los defectos se documentan y se devuelven al GAM como observaciones, no se corrigen en silencio.

**CERRADA — 2026-07-12.** Quedaron operativos el modelo versionado, la importación asíncrona, el preview y la activación/reconciliación atómica. Uyuni v1 está activa con 35.013 objetos en 7 capas y 11.985 predios reconciliados; PRs #3–#6, migraciones M010–M013 y suite completa 223/223.

---

### FASE 2 — Visor geográfico institucional

**Objetivo:** reemplazar la demo estática por el visor dinámico integrado al sistema — la pieza más visible para los GAMs.

**Entregables:**
- T-2.1: Endpoints de tiles vectoriales desde PostGIS (`ST_AsMVT`) servidos por la propia API .NET — sin servicios adicionales que complicar el despliegue. A esta escala (≈12k predios) es sobradamente suficiente.
- T-2.2: Componente de mapa en Blazor WASM (MapLibre GL JS vía interop): capas conmutables (las 7), estilo por capa.
- T-2.3: Búsqueda por `zona+manzana+lote` y por triplete canónico, con zoom al predio.
- T-2.4: Ficha de predio al clic: datos alfanuméricos, superficie gráfica vs. declarada, versión de dataset.
- T-2.5: Impresión de croquis simple del predio (base para el certificado de Fase 4).

**Criterio de cierre:** demostración funcional grabable — visor cargando las 7 capas desde la BD versionada, búsqueda encontrando un predio real de Uyuni, ficha correcta contra verificación SQL manual del mismo predio. Tests de los endpoints MVT y de búsqueda verdes.

**Hito comercial:** al cerrar esta fase tienes una demo en vivo muy superior a la exposición de junio. Vale la pena agendar una segunda presentación con Uyuni aquí.

---

### FASE 3 — Motor de valuación

**Objetivo:** valuación conforme a la Guía Nacional 2024, con Fase B lista para activarse el día que exista ordenanza.

**Entregables:**
- T-3.1: Modelo de zonas homogéneas con valor unitario de terreno por zona (Fase A) — versionable por gestión fiscal (los valores cambian por año).
- T-3.2: Cálculo de valor de terreno por predio: superficie × Vz, con factores de ajuste que la norma contemple (forma, topografía, ubicación en manzana — según Guía de Zonificación 2024).
- T-3.3: Motor paramétrico Fase B: tabla de tipologías constructivas con valor/m² **configurable**, depreciación por antigüedad y estado de conservación. Se puebla con tabla de prueba marcada como NO OFICIAL.
- T-3.4: Valuación total del predio = terreno + construcciones (cuando Fase B esté activa), con vigencia por gestión.
- T-3.5: Recalculo masivo por gestión (job asíncrono, reutilizando la infraestructura de la Fase 1).

**Criterio de cierre:** (a) valuación Fase A calculada para los 11,985 predios de Uyuni con la salida SQL del recalculo masivo; (b) verificación manual de 5 predios contra cálculo a mano; (c) Fase B demostrada con tabla de prueba y flag de activación funcionando; (d) tests verdes.

**Decisión pendiente DP-02:** valores de zona actualizados de Uyuni (hoy solo se conoce zona C = 88 Bs/m²). Disparador: solicitar formalmente al GAM la tabla completa de valores de zona vigente al iniciar esta fase.

---

### FASE 4 — Certificados catastrales

**Objetivo:** el primer producto "vendible" completo: el GAM emite certificados oficiales desde el sistema.

**Entregables:**
- T-4.1: ADR de librería PDF (evaluar QuestPDF — verificar términos de licencia Community vs. comercial — contra alternativas MIT puras). La licencia importa: esto se venderá.
- T-4.2: Plantilla de certificado conforme al Reglamento Nacional: datos técnicos, croquis (reutiliza T-2.5), valuación vigente, colindancias si la capa lo permite.
- T-4.3: Folio correlativo por gestión y por municipio; registro de emisión inmutable (misma disciplina que la auditoría).
- T-4.4: Código QR + endpoint público de verificación (`/verificar/{codigo}`) que muestra validez y datos mínimos — este endpoint es también la semilla del portal ciudadano.
- T-4.5: Control de acceso: solo roles autorizados emiten. (Si la autenticación/roles aún no existe formalmente, se construye aquí como T-4.0 — se confirmará con el snapshot de inicio de fase.)

**Criterio de cierre:** PDF real de certificado de un predio de Uyuni generado por el sistema, QR escaneado resolviendo a la verificación pública correcta, registro de emisión visible por SQL, intento de emisión sin rol autorizado rechazado (evidencia del 401/403), tests verdes.

---

### FASE 5 — Trámites catastrales internos

**Objetivo:** el sistema deja de ser un repositorio de datos y se convierte en la herramienta operativa diaria del técnico municipal.

**Entregables:**
- T-5.1: Máquina de estados genérica de trámite (iniciado → en revisión → aprobado/rechazado/anulado) con guardas — mismo patrón que el agregado `Importacion`.
- T-5.2: Trámite de transferencia (cambio de titular) con documentos de respaldo en MinIO.
- T-5.3: Trámite de actualización de mejoras (alta/modificación de construcciones) — alimenta directamente la valuación Fase B.
- T-5.4: Trámites de subdivisión y fusión: los más delicados porque crean/extinguen predios. Definir política de asignación de nuevos `cod_pred` con el GAM (**DP-03**, disparador al iniciar T-5.4). El efecto geométrico se registra; la edición geométrica avanzada puede apoyarse en flujo QGIS → re-importación parcial documentada, sin construir un editor CAD web (fuera de alcance v1 — explícitamente).
- T-5.5: Bandeja de trámites por rol, historial por predio.

**Criterio de cierre:** un trámite de cada tipo ejecutado end-to-end contra datos reales de Uyuni con evidencia de cada transición de estado en la auditoría (SQL), documentos recuperables desde MinIO, y regla verificada de que ningún trámite no-aprobado altera datos del predio. Tests verdes.

---

### FASE 6 — Integración tributaria

**Objetivo:** cerrar el ciclo catastro → valuación → impuesto, que es lo que económicamente le importa al alcalde.

**Entregables:**
- T-6.1: Resolución de **DP-01**: reunión técnica con GAM Uyuni — ¿liquidan IPBI vía RUAT o localmente? El diseño ya contempla ambos.
- T-6.2: Cálculo de base imponible IPBI por predio desde la valuación vigente (escala de Ley 843 / ordenanza municipal, parametrizable por gestión).
- T-6.3a (camino RUAT): exportación en el formato que RUAT requiera (a investigar en T-6.1).
- T-6.3b (camino propio): liquidación anual por predio con descuentos por pronto pago parametrizables, y reporte de emisión masiva.
- T-6.4: Reportes gerenciales: base imponible total del municipio, comparativa por gestión, predios sin valuación.

**Criterio de cierre:** liquidación (o export RUAT) generada para la totalidad de predios valuados de Uyuni, verificación manual de 5 casos, reportes correctos contra SQL, tests verdes.

---

### FASE 7 — Portal ciudadano

**Objetivo:** cara pública del sistema; argumento político fuerte para los GAMs.

**Entregables:**
- T-7.1: Sitio público (mismo Blazor WASM u hosting estático + endpoints públicos de solo lectura, con rate limiting): consulta de predio por identificador humano mostrando solo datos no sensibles (**DP-04:** definir con el GAM qué es público — nombres de titulares probablemente NO, por protección de datos).
- T-7.2: Verificación de certificados (ya existe desde T-4.4; aquí se le da interfaz pública pulida).
- T-7.3: Consulta de estado de trámite por código de seguimiento.
- T-7.4: Endurecimiento: los endpoints públicos no exponen nada más que lo definido; revisión de seguridad específica (prompt dedicado a Codex de revisión adversarial).

**Criterio de cierre:** portal accesible sin autenticación mostrando solo lo permitido (evidencia: intento de acceso a datos sensibles rechazado), verificación QR funcionando desde un teléfono real, rate limiting demostrado, tests verdes.

---

### FASE 8 — Incorporación de Caranavi (pipeline CAD → catastro)

**Objetivo:** demostrar que el sistema es multi-municipio de verdad, resolviendo el caso duro: datos en CAD.

*(Puede adelantarse en paralelo desde la Fase 5 si conviene comercialmente: el pipeline CAD es mayormente trabajo de datos, no de código nuevo.)*

**Entregables:**
- T-8.1: Diagnóstico del CAD de Caranavi: formato (DWG/DXF), ¿está georreferenciado y en qué sistema?, ¿capas separadas o dibujo plano?, ¿tiene atributos o solo geometría? Este diagnóstico define todo lo demás. **(DP-05: obtener los archivos CAD del GAM Caranavi — disparador inmediato, puedes gestionarlo ya.)**
- T-8.2: Pipeline de conversión documentado y repetible: DXF → GDAL/ogr2ogr → limpieza topológica (geometrías válidas, cierre de polígonos, eliminación de duplicados) → asignación de estructura de capas del sistema → SHP/GeoPackage listo para el importador de la Fase 1. QGIS como herramienta de control de calidad visual.
- T-8.3: Plan de levantamiento y digitalización para lo que el CAD no cubra: alcance, priorización por zonas, formato de captura en campo compatible con el importador, estimación de esfuerzo — documento entregable al GAM Caranavi (también es tu herramienta de venta del servicio de levantamiento).
- T-8.4: Multi-tenancy verificado: instancia/BD separada por municipio según ADR 0047 (una BD por GAM — aislamiento total, el modelo más seguro para datos catastrales). Caranavi importado como su propia instancia con su propia versión 1.
- T-8.5: Codificación canónica de Caranavi: verificar que existe (o definir con el GAM) la estructura `(cod_uv, cod_man, cod_pred)` — **no asumir que la convención de Uyuni aplica** (**DP-06**).

**Criterio de cierre:** instancia Caranavi levantada con los datos convertidos del CAD importados como versión 1, visor mostrando Caranavi, reporte de cobertura (qué % del territorio quedó digitalizado y qué requiere levantamiento), documento de plan de levantamiento entregado.

---

### FASE 9 — Producción, seguridad y despliegue multi-municipio

**Objetivo:** de "funciona en mi máquina y en demo" a "un GAM opera esto todos los días sin que Saul esté presente".

**Entregables:**
- T-9.1: Imagen de despliegue única y guion de instalación por municipio (`docs/DESPLIEGUE.md`): compose de producción, Caddy con TLS automático, MinIO, PostGIS, variables por municipio. Objetivo medible: **municipio nuevo desplegado en menos de un día.**
- T-9.2: Respaldos automatizados (pg_dump + snapshot MinIO) con restauración **probada** — un respaldo no probado no es un respaldo. Evidencia: restauración completa en entorno limpio.
- T-9.3: Monitoreo básico: healthchecks, alertas de disco/BD, log centralizado por instancia.
- T-9.4: Endurecimiento: usuarios de BD con privilegios mínimos, rotación de secretos documentada, HTTPS obligatorio, revisión de dependencias (`dotnet list package --vulnerable`).
- T-9.5: Selección de proveedor VPS para el modelo "hosting administrado por Saul" (**DP-07**: cotizar 2–3 proveedores con presencia/latencia razonable para Bolivia; definir specs mínimas por municipio y el precio que cargas en tu planilla de costos). Para GAMs con presupuesto propio: el mismo guion T-9.1 aplica a su infraestructura.
- T-9.6: Manual de usuario por rol (técnico catastral, ventanilla, administrador) y guion de capacitación de medio día.

**Criterio de cierre:** despliegue completo desde cero en un VPS real cronometrado (< 1 día), restauración de respaldo demostrada, checklist de seguridad completado con evidencia, manuales commiteados.

---

### FASE 10 — Producto en operación y crecimiento

**Objetivo:** convertir el sistema en operación sostenible y preparar la incorporación de más personas (tu punto 8).

**Entregables:**
- T-10.1: Piloto en producción real con Uyuni: convenio formal, datos oficiales, usuarios municipales capacitados, período de acompañamiento definido (p. ej. 60 días con soporte intensivo).
- T-10.2: Canal de soporte e incidencias (aunque sea un flujo simple al inicio) con SLA interno.
- T-10.3: Documentación de onboarding para nuevos desarrolladores: el propio rigor del proyecto (ADRs 0026–0049+, tests E2E, este plan) ya es la mitad del onboarding.
- T-10.4: Ciclo de mantenimiento: cadencia de actualizaciones, política de versiones del producto, ventana de mantenimiento acordada con los GAMs.
- T-10.5: Expansión comercial: dossier de producto con resultados reales de Uyuni (predios gestionados, certificados emitidos, recaudación calculada) — los números del piloto son el argumento de venta para el siguiente municipio.

**Criterio de cierre (del proyecto entero):** un GAM emitiendo certificados y liquidaciones reales desde el sistema en su operación diaria, un segundo municipio (Caranavi) con datos cargados y plan de levantamiento en marcha, y el sistema desplegable en < 1 día para el tercero. Ese es el estándar de "mejor sistema catastral municipal de Bolivia": no un eslogan, sino ciclo completo + trazabilidad + replicabilidad.

---

## 6-bis. Registro de pendientes técnicos

### M-LECTOR-1 — Geometrías que NTS no logra construir

**RESUELTA — 2026-07-14.** `ShapefileReader` conserva ahora la geometría cruda
cuando el lector estricto de NTS falla y el fallback
`IgnoreInvalidShapes` logra construirla. Los 32 registros recuperados —30
edificaciones y 2 manzanas— se clasifican como O1 con geometría y razón de
invalidez persistidas; los nulos genuinos permanecen separados como O4. La
política completa está documentada en el ADR 0053.

Evidencia de cierre:

1. Gate de persistencia de cinco celdas: 32 O1 exactos, todos con geometría no
   nula y razón; 28 O4 exactos; O2=O3=0; 35.013 objetos; manzana fila 125 con
   `Too few points in geometry component`; igualdad textual v2/v3, incluidos
   `JOSÉ EDUARDO PEREZ` y `JOSÉ EDUARDO PÉREZ`.
2. Comparación final: v2 archivada y v3 activa; conteos idénticos en las siete
   capas; 60 O4 en v2 frente a 32 O1 + 28 O4 en v3.
3. Comparación exhaustiva por `(capa, fila_origen)`: 35.013 pares, cero filas
   exclusivas y cero atributos distintos.
4. Suite backend ampliada de 223 a 224 tests con
   `PostVersion_GeometriasInvalidasRecuperables_SeparaO1DeNulosGenuinosO4`.

### DT-LECTOR-2 — Codificación DBF sin descubrimiento de `.cpg`

**Pendiente — severidad baja.** El camino de atributos construye directamente
`new DbfReader(rutaDbf)` en
`src/backend/SG.Infrastructure/Importacion/ShapefileReader.cs:24-27`; por ello
no usa el descubrimiento de `.cpg` disponible en el lector SHP configurado en
las líneas 28-32. Debe integrarse una política explícita de encoding para DBF
sin alterar la lectura geométrica.

No es un bloqueante actual: el barrido de mojibake sobre las 34 columnas de
texto devolvió cero filas afectadas y la comparación v2/v3 confirmó igualdad
en los 35.013 pares. Se mantiene como deuda técnica para una entrega futura con
codificación DBF distinta.

### DT-CI-1 — Integración con Testcontainers fuera de CI

El job actual de CI ejecuta únicamente los tests de dominio y aplicación. Los
41 tests de integración con PostgreSQL/PostGIS real mediante Testcontainers se
ejecutan localmente. Se debe evaluar su activación en GitHub Actions.
**Agendada: Fase 9 o antes.**

### Limpiezas menores

- El seeder de perfiles es solo aditivo y no reconcilia perfiles divergentes.

## 6. Registro de decisiones pendientes (nada colgando)

| ID | Decisión | Disparador | Responsable de gestionar |
|---|---|---|---|
| DP-01 | IPBI vía RUAT vs. liquidación propia | Inicio Fase 6 — reunión técnica con GAM Uyuni | Saul |
| DP-02 | Tabla completa de valores de zona de Uyuni | Inicio Fase 3 — solicitud formal al GAM | Saul |
| DP-03 | Política de asignación de `cod_pred` en subdivisiones | Inicio T-5.4 — acordar con GAM | Saul |
| DP-04 | Qué datos son públicos en el portal ciudadano | Inicio Fase 7 — acordar con GAM | Saul |
| DP-05 | Obtener archivos CAD de Caranavi | **Inmediato** — se puede gestionar ya | Saul |
| DP-06 | Codificación canónica de Caranavi | T-8.1, tras diagnóstico del CAD | Saul + Claude |
| DP-07 | Proveedor VPS y precios del hosting administrado | Inicio Fase 9 | Saul |
| DP-08 | Ordenanza municipal de valores por tipología (activa Fase B de valuación) | Externo/político — el sistema no lo espera (motor paramétrico, T-3.3); recordatorio en cada contacto con el GAM | Saul |

## 7. Riesgos globales

**R-1. Dependencia de una sola persona (tú).** Mitigación: la disciplina documental del proyecto (ADRs, tests, este plan, manuales de Fase 9) es el seguro de vida del proyecto; T-10.3 lo formaliza.

**R-2. Calidad de datos de origen desconocida (6 capas de Uyuni, todo Caranavi).** Mitigación: previews con reporte antes de confirmar (Fase 1), diagnóstico CAD antes de comprometer alcance (T-8.1), defectos documentados y devueltos al GAM como observaciones — nunca corregidos en silencio.

**R-3. Ciclo político municipal.** Los interlocutores de hoy pueden cambiar. Mitigación: buscar convenio formal en T-10.1 apenas haya producto demostrable (tras Fase 4 ya es viable); los hitos comerciales de las Fases 2 y 4 existen justo para acelerar ese compromiso.

**R-4. Alcance en expansión (scope creep).** Con 7 módulos, el riesgo de empezar todo y cerrar nada es real. Mitigación: la regla de cierre secuencial del §1.4 es innegociable — no se abre una fase con la anterior sin criterio de cierre cumplido.

**R-5. Bloqueo normativo de Fase B.** Ya neutralizado por diseño (motor paramétrico, DP-08).

**R-6. Costos de tokens/créditos de las IAs.** Mitigación: §1.5; además, a medida que el sistema madura, la proporción de trabajo repetitivo baja y la de decisiones sube — el triángulo se vuelve más barato por fase, no más caro.

## 8. Gobernanza de calidad (vigente en todas las fases)

Estas reglas ya son ley del proyecto y este plan las ratifica: tests E2E contra PostGIS real (nunca solo mocks para lo geoespacial); CI verde como condición de merge; un ADR por cada decisión arquitectónica; auditoría append-only intocable (guarda C# + `trg_auditoria_immutable`); comando de commit obligatorio sin trailers; staging por archivo, jamás `git add .`; `information_schema` para verificación de esquema; cláusula de no-exposición de contraseñas en todo prompt que toque configuración; evidencia antes que opinión, siempre.

---

## 9. Siguiente acción inmediata

1. **Saul:** enviar el primer snapshot (formato del §1.2) para confirmar la línea base del §2.
2. **Claude:** con el snapshot validado, redactar el **Prompt Codex #001 — Fase 0 completa** (T-0.1 a T-0.4 en un solo lote coherente).
3. **Saul (en paralelo, sin costo técnico):** gestionar DP-05 (archivos CAD de Caranavi) — cuanto antes lleguen, antes se diagnostica el caso duro.

---

*Este documento se versiona en el repositorio (`docs/PLAN_MAESTRO.md`). Toda modificación de alcance u orden de fases se registra aquí con fecha y motivo. Última actualización: 2026-07-12, v1.1 — cierre de Fases 0 y 1.*
