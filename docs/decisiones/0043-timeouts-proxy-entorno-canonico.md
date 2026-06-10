# ADR 0043 — Timeouts de proxy y entorno local canónico

**Fecha**: 2026-06-10
**Estado**: Aceptado
**Sprint**: 4 — item 5 (deuda técnica de Sprint 3)
**Autor**: Saul Gutierrez

---

## Contexto

En Sprint 3 se identificó que el endpoint de confirmación de importación
(`POST /api/importaciones/{id}/confirmar`) tardaba aproximadamente 34 segundos
al procesare ~11 000 predios durante las pruebas in-process. La tarea del
Sprint 4 era diagnosticar si Caddy imponía un timeout sobre esa ruta y
configurar valores explícitos.

Al iniciar el diagnóstico se descubrió una condición más fundamental: **la
ruta proxy nunca había existido en producción local**. La cadena completa
cliente → Caddy → Kestrel nunca fue ejercida durante el Sprint 3 ni antes.

---

## Hallazgo 1 — La ruta proxy nunca existió hasta hoy

El `Caddyfile.local` fue creado en Sprint 0 con el upstream `api:8080`
configurado, pero el contenedor `sg_api` nunca fue levantado (la imagen nunca
fue construida). Al arrancar el stack con `start-local.sh` se obtenía 502
porque Caddy intentaba alcanzar un upstream inexistente.

Todo el trabajo de Sprint 3 (tests de integración, medición de la importación)
usó `WebApplicationFactory<Program>` — transporte in-process sin HTTP real,
sin Caddy, sin red Docker. Esa medición de ~34s es válida como línea base del
handler pero **no refleja el tiempo de la ruta de producción**.

Caddy 2 tiene por defecto `dial_timeout=3s` y el resto de timeouts a 0
(ilimitado). Sin timeouts explícitos, el Caddyfile de Sprint 0 habría dejado
pasar la importación sin corte — pero el upstream inexistente hacía la
discusión de timeouts irrelevante.

---

## Decisión 1 — Timeouts explícitos como contrato en Caddyfile.local

Se configuraron los siguientes timeouts en el bloque `handle /api/*`:

```caddy
handle /api/* {
  reverse_proxy api:8080 {
    transport http {
      dial_timeout 5s
      response_header_timeout 120s
    }
  }
}
```

**`dial_timeout 5s`**: si `sg_api` no está levantado, Caddy falla rápido con
502 en lugar de colgar indefinidamente. Correcto para detección de upstream
caído.

**`response_header_timeout 120s`**: tiempo máximo para recibir los headers de
respuesta desde Kestrel. Es el timeout relevante para operaciones síncronas
largas como la confirmación de importación. 120s es deliberadamente generoso
porque este timeout es un **puente** — la solución estructural es la
importación asíncrona (item 4), no reducir el umbral de Caddy.

Estos valores son un contrato explícito: quien cambie los timeouts debe
hacerlo aquí de forma consciente, no depender de defaults de Caddy.

---

## Mediciones — Las tres cifras

Las mediciones se realizaron el 2026-06-10 con el stack Docker completo
(`sg_postgres`, `sg_api`, `sg_caddy` en `sg_network`), dataset: Uyuni
predios (`dat_pre_uyu.zip`, 3.6 MB, 11 985 filas).

| Escenario | Entorno | Operación | Tiempo | Notas |
|---|---|---|---|---|
| Sprint 3 (referencia) | In-process (WebApplicationFactory) | Confirmación ~11 000 creates | ~34s | Estimado con `time` de shell; sin red HTTP, sin Caddy, sin stack Docker |
| Hoy — creates vía proxy | Docker compose (prod-local) | 11 985 creates | **11.37s** | **Número operativo de referencia** |
| Hoy — updates vía proxy | Docker compose (prod-local) | 11 983 updates | 12.3s | Re-importación idempotente |

**Los ~34s y los 11.37s NO son comparables.** Miden entornos fundamentalmente
distintos:

- El ~34s (Sprint 3) se midió con `WebApplicationFactory<Program>`: transporte
  in-process, sin HTTP real, sin Caddy, sin red Docker, PostgreSQL en el host
  (puerto 5434). Es útil para verificar correctitud del handler, no para
  estimar tiempos de producción.
- El 11.37s se midió con `curl --max-time 300` contra `http://localhost/api/...`,
  pasando por Caddy (`:80`), la red `sg_network`, y PostgreSQL dentro del
  mismo stack Docker. Es el tiempo que experimenta el cliente real.

Interpretar la diferencia como "el proxy mejoró el rendimiento" sería incorrecto.
Las variables de entorno son demasiado distintas para aislar causas. **Los
números operativos de referencia son 11.37s (creates) y 12.3s (updates).**
El ~34s se archiva como evidencia de Sprint 3, no como cota del sistema.

**Consecuencia de timeout**: con `response_header_timeout=120s` y el número
operativo medido de 11.37s, el margen de seguridad es **~10.5×**. El timeout
de 120s no intervendrá en condiciones normales con datasets del tamaño de
Uyuni.

---

## Decisión 2 — Reevaluación de prioridad del item 4 (importación asíncrona)

El item 4 (respuesta `202 Accepted` + polling) fue planificado para resolver
el riesgo de timeout en la ruta proxy. Con las mediciones actuales:

- El riesgo de timeout con el dataset de Uyuni (11 985 predios) es bajo:
  margen de ~10.5× sobre el `response_header_timeout=120s`.
- Extrapolación lineal (11.37s / 11 985 predios ≈ 0.95ms/predio):
  el timeout de 120s se alcanzaría cerca de **~126 000 predios**.
  Municipios grandes en Bolivia (Cochabamba, Santa Cruz) pueden superar
  ese umbral en catastros completos.
- El piloto de Caranavi (<15 000 predios estimados) está bien dentro del
  margen; sin riesgo de timeout a corto plazo.

**Conclusión**: el item 4 permanece en **prioridad media**. Es valioso para
escalabilidad y UX (el cliente no queda bloqueado durante importaciones
grandes), pero no bloquea el Sprint 4 ni el piloto de Caranavi. La
extrapolación lineal sitúa el riesgo operativo en ~120 000 predios.

---

## Decisión 3 — Stack Docker Compose como entorno local canónico

A partir de Sprint 4, el entorno de referencia para pruebas manuales e
integraciones es el stack Docker (`docker compose ... up`), no `dotnet run`
contra un PostgreSQL del host.

Razones:
- Las pruebas in-process (WebApplicationFactory) verifican correctitud del
  código, pero no la ruta real cliente → Caddy → Kestrel → PostgreSQL.
- El stack Docker reproduce fielmente el entorno de despliegue municipal.
- `dotnet run` queda reservado para debugging puntual de código (breakpoints,
  hot reload).

---

## Trampas conocidas

### (a) `\dt` sin search_path no ve schemas de aplicación

El comando `\dt` en psql muestra solo las tablas en el `search_path` actual
(por defecto `public`). El schema de la aplicación usa `dominio`, `identidad`
y `auditoria`. En esta sesión, una consulta `\dt` mostró solo las tablas de
PostGIS (`spatial_ref_sys`, `topology.*`), generando la falsa impresión de que
la base estaba vacía — cuando en realidad tenía 11 985 predios.

**Siempre usar:**
```sql
SELECT table_schema, table_name
FROM information_schema.tables
WHERE table_schema NOT IN ('pg_catalog','information_schema','topology')
ORDER BY table_schema, table_name;
```
o bien `\dt *.*` para ver todos los schemas.

### (b) Los GUIDs de perfiles_importacion cambian tras un reset

Los perfiles sembrados por `DomainSeeder.SeedPerfilesImportacionAsync` usan
`Guid.NewGuid()` en cada inicialización — no son determinísticos. Tras un
reset del volumen `sg_postgres_data`, los IDs de los perfiles cambian y
cualquier referencia hardcodeada a un GUID específico queda inválida.

Mitigación: obtener el ID dinámicamente:
```sql
SELECT id FROM dominio.perfiles_importacion WHERE nombre='uyuni-predios';
```

### (c) Deuda — Seeder usa Guid.NewGuid(): IDs de perfiles no deterministas

`DomainSeeder.SeedPerfilesImportacionAsync` llama `Guid.NewGuid()` en cada
inicialización. Tras un reset del volumen o un despliegue en un nuevo municipio,
los IDs de los perfiles cambian. Cualquier referencia externa hardcodeada a un
GUID específico (scripts de importación, configuración de cliente, modelo
multi-municipio en L4) queda inválida.

**Impacto actual**: bajo (sistema de un solo municipio, no hay referencias
externas). **Impacto futuro**: alto en modelo multi-municipio donde distintos
nodos pueden tener IDs distintos para el mismo perfil.

**Fix pendiente**: usar GUIDs constantes en el seeder (hardcodeados por perfil
bien conocido) o resolver siempre por slug (`nombre='uyuni-predios'`) en lugar
de por ID. Este ítem se registra como deuda técnica de Sprint 4 (nivel L4,
baja prioridad para el piloto de Caranavi, alta para escalado multi-municipio).

Workaround actual:
```sql
SELECT id FROM dominio.perfiles_importacion WHERE nombre = 'uyuni-predios';
```

### (d) Deuda menor: caché de NuGet en el Dockerfile

El `dotnet restore` dentro del Dockerfile tarda ~550s en el primer build por
ausencia de caché local en el runner. En este build se observó además un
timeout de red de 60s en un paquete (`Microsoft.AspNetCore.Http.Abstractions`)
con recovery automático. La solución estándar es montar un volumen de caché
de NuGet en el Dockerfile:

```dockerfile
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore SG.Api/SG.Api.csproj
```

Esta mejora queda pendiente como deuda menor; no bloquea nada.

---

## Consecuencias

- El Caddyfile.local tiene timeouts explícitos y documentados. Cualquier
  cambio futuro debe actualizarse aquí.
- El tiempo de confirmación en el entorno canónico (11.37s) está bien dentro
  del timeout de 120s y no requiere acción inmediata en item 4.
- Las pruebas in-process siguen siendo válidas para correctitud funcional,
  pero las mediciones de performance deben hacerse vía el stack Docker.
- El número ~34s de Sprint 3 se mantiene como referencia conservadora para
  planeación de escalabilidad.

---

## Archivos modificados

| Archivo | Cambio |
|---|---|
| `infra/docker/caddy/Caddyfile.local` | Timeouts explícitos en `transport http`, comentario de cabecera actualizado |

---

*Refs: Sprint 4 item 5, Sprint 3 PC 3.2 (medición in-process ~34s), item 4 (importación asíncrona — prioridad reevaluada)*
