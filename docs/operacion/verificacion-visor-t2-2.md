# Verificación funcional del visor T-2.2

Esta guía verifica literalmente el flujo `login -> mapa -> siete capas ->
tiles autenticados -> revalidación ETag -> expiración de sesión`. Debe
ejecutarse desde PowerShell, en la raíz del repositorio, con Docker Desktop
operativo y un `.env` válido. No se deben copiar tokens ni contraseñas a la
evidencia.

## 1. Confirmar rama y árbol de trabajo

Ejecutar:

```powershell
git branch --show-current
git status --short
```

Resultado esperado:

- La rama es `feature/fase2-visor-blazor`.
- El árbol está limpio cuando se ejecute la evidencia posterior a los commits.

Criterio de fallo: rama distinta o cambios locales no explicados.

## 2. Reconstruir la imagen web y desplegar el stack

Ejecutar las invocaciones canónicas, con `.env` explícito y ambos archivos
Compose:

```powershell
docker compose --env-file "$PWD\.env" -f "$PWD\infra\docker\docker-compose.yml" -f "$PWD\infra\docker\docker-compose.local.yml" build web
docker compose --env-file "$PWD\.env" -f "$PWD\infra\docker\docker-compose.yml" -f "$PWD\infra\docker\docker-compose.local.yml" up -d
```

Resultado esperado:

- El primer comando termina con `Image sg-catastro-web Built`.
- El segundo crea o actualiza `postgres`, `minio`, `api`, `web` y `caddy`.
- No se usa `docker start`: el código nuevo debe quedar dentro de una imagen
  reconstruida.

Criterio de fallo: error de build, servicio omitido, credenciales vacías por
no usar `--env-file`, o arranque mediante una imagen anterior.

## 3. Verificar salud completa antes de abrir el navegador

Esperar a que terminen los `start_period` y ejecutar:

```powershell
docker compose --env-file "$PWD\.env" -f "$PWD\infra\docker\docker-compose.yml" -f "$PWD\infra\docker\docker-compose.local.yml" ps
docker inspect --format '{{.State.Health.Status}}' sg_postgres
docker inspect --format '{{.State.Health.Status}}' sg_minio
docker inspect --format '{{.State.Health.Status}}' sg_api
docker inspect --format '{{.State.Health.Status}}' sg_web
docker inspect --format '{{.State.Status}}' sg_caddy
curl.exe --silent --show-error --fail-with-body --dump-header - http://localhost/health --output NUL
curl.exe --silent --show-error --fail-with-body --dump-header - http://localhost/ --output NUL
$indicePublicado = curl.exe --silent --show-error --fail-with-body http://localhost/
$coincidenciaRuntime = [regex]::Match($indicePublicado, 'src="(?<ruta>_framework/blazor\.webassembly\.[^"]+\.js)"')
if (-not $coincidenciaRuntime.Success) { throw "index.html no referencia el runtime Blazor con fingerprint." }
$rutaRuntime = $coincidenciaRuntime.Groups['ruta'].Value
$headersRuntime = (curl.exe --silent --show-error --fail-with-body --dump-header - "http://localhost/$rutaRuntime" --output NUL) -join "`n"
Write-Output "runtime=$rutaRuntime"
Write-Output $headersRuntime
if ($headersRuntime -match '(?im)^Content-Type:\s*text/html') { throw "El runtime Blazor recibió el fallback HTML." }
if ($headersRuntime -notmatch '(?im)^Content-Type:\s*(text|application)/javascript') { throw "Content-Type inesperado para el runtime Blazor." }
node scripts/verificar-mapa-publicado.mjs http://localhost/js/mapa.js
```

Resultado esperado:

```text
sg_postgres  healthy
sg_minio     healthy
sg_api       healthy
sg_web       healthy
sg_caddy     running
```

Los tres recursos HTTP deben devolver `HTTP/1.1 200`. La raíz debe incluir
`Content-Type: text/html`; el artefacto fingerprinted extraído del `index.html`
debe incluir `Content-Type: text/javascript` o `application/javascript`, nunca
`text/html`. La sonda JavaScript debe terminar con `capas_entrada=7`,
`fuentes_agregadas=7`, `capas_dibujadas=16`, `encuadres_aplicados=1`, el bbox
esperado en `encuadre_limites` y las opciones `padding: 40, maxZoom: 14` en
`encuadre_opciones`. También debe mostrar `dimensiones_iniciales=0x0`,
`encuadres_antes_layout=0`, `fuentes_antes_layout=0`,
`encuadre_dimensiones=1078x712` y un `zoom_final` mayor que 10. La sonda
reproduce un estilo ya cargado y un contenedor que adquiere dimensiones después
del primer frame; demuestra que fuentes, capas y encuadre esperan el layout y
que la cámara final no depende de una animación pendiente. Antes de esas
comprobaciones debe registrar `mapa_js_cache_control=no-cache`, un ETag no vacío,
`mapa_js_revalidacion_status=304` y `no-cache` para `appsettings.json`,
`app.css` e `index.html`. Esto prueba que los recursos propios sin huella se
revalidan después de cada despliegue aunque exista una copia en disco. Si
`CADDY_HTTP_PORT` no es 80, sustituir `localhost` por
`localhost:<puerto>` tanto en los `curl` como en el argumento de la sonda, y
registrar el valor usado.

La misma sonda debe listar siete valores `fuente_<capa>_template`, todos
absolutos y same-origin, con los placeholders literales `/{z}/{x}/{y}.mvt`;
debe cuadrar los rangos `minzoom` del catálogo y `maxzoom=22`. Además debe
expandir un tile de `distritos` calculado desde el centro del bbox publicado y
terminar con `transform_tile_autorizada=true` y
`transform_no_tile_autorizada=false`. La sonda no realiza login ni expone el
Bearer: ejerce directamente el callback publicado con un token ficticio para
verificar que MapLibre recibe una URL absoluta y que el encabezado sólo se
inyecta en `/api/tiles/`.

La sonda también debe registrar seis contratos `css_layout_*=true`, sin un
selector `:deep(...)` residual en el CSS aislado publicado. Para un viewport
simulado de `1366x768`, debe mostrar `alto_disponible_mapa=712`,
`proporcion_alto_mapa=0.9271` y `mapa_resize_aplicados=1`. Esto comprueba que la
cadena `html/body/#app -> .aplicacion -> main -> .pagina-visor -> .visor`
propaga el alto disponible, que el mapa supera el 70% del viewport y que el
`ResizeObserver` llama a `map.resize()` cuando el contenedor pasa de `0x0` a su
alto definitivo.

Criterio de fallo: cualquier health distinto de `healthy`, Caddy distinto de
`running`, reinicios continuos, respuesta HTTP distinta de 200, referencia
literal sin fingerprint, runtime inexistente o fallback HTML entregado para un
artefacto `_framework`, `mapa.js` servido como HTML, conteos de la sonda
distintos de 7/7/16, inicialización antes de adquirir dimensiones, zoom final
menor o igual que 10, ausencia de `Cache-Control: no-cache`/ETag, revalidación
distinta de 304, ausencia/diferencia del encuadre esperado, plantilla de tile
relativa o sin placeholders XYZ, rango de fuente distinto del catálogo, Bearer
ausente en el tile ficticio, filtrado a un recurso que no sea tile, contrato CSS
en `false`, selector `:deep(...)` residual, proporción de alto menor o igual a
0,70 o ausencia del reajuste del mapa.

## 4. Preparar la captura del navegador

1. Abrir `http://localhost/`.
2. Abrir las herramientas de desarrollo y seleccionar **Network/Red**.
3. Activar **Preserve log/Conservar registro**.
4. Mantener **Disable cache/Deshabilitar caché** desmarcado; la revalidación
   condicional necesita la caché HTTP.
5. Filtrar inicialmente por `login`.

Resultado esperado: aparece el formulario institucional de ingreso y no hay
errores en consola. En Network, `js/mapa.js`, `css/app.css` y
`appsettings.json` muestran `Cache-Control: no-cache`; una copia en disk cache
debe producir una solicitud condicional y no reutilizarse sin revalidación.

Criterio de fallo: pantalla en blanco, error Blazor, 404 de `_framework`,
respuesta HTML para un artefacto JavaScript, MapLibre cargado desde un CDN o
error JavaScript; también falla si un asset propio sin huella se sirve desde
disk cache sin una solicitud condicional al servidor.

## 5. Iniciar sesión

1. Usar un usuario configurado por el orquestador con rol `Admin` o `Tecnico`.
2. Ingresar la contraseña sin mostrarla en capturas.
3. Pulsar **Ingresar**.
4. Inspeccionar `POST /api/auth/login` en Network sin capturar Request Payload
   ni valores de tokens.

Resultado esperado:

- `POST /api/auth/login` devuelve 200.
- La aplicación navega a `/visor` sin recarga completa.
- La vista encuadra Uyuni usando el bbox de `wwwroot/appsettings.json`.
- En Application/Storage no aparecen claves de sesión en `localStorage` ni
  `sessionStorage`.

Criterio de fallo: estado distinto de 200, usuario autorizado rechazado,
refresh token persistido, token en URL o token en almacenamiento del navegador.

## 6. Verificar las siete capas conmutables

1. Confirmar que el panel enumera exactamente: Distritos, Zonas, Manzanas,
   Parcelas, Predios no fotografiados, Edificaciones y Vías.
2. En la vista inicial comprobar que parcelas y edificaciones no generan
   solicitudes a zoom bajo.
3. Acercar el mapa hasta zoom 16 sobre el centro urbano.
4. Desactivar y volver a activar cada capa, una por una.
5. En Console comprobar las trazas `[SG.Web mapa]`: creación con
   `cantidadCapas: 7` y el bbox configurado, `encuadre aplicado` con `center` y
   `zoom`, `w` y `h`, inicialización, siete mensajes `addSource` con
   `urlTemplate` absoluta y placeholders XYZ, `capas listas` con `fuentes: 7`,
   y mensajes `transformRequest` con `resourceType: "Tile"`, URL absoluta y
   `autenticada: true`.
6. En Network o en la consola comprobar que existen recursos cuya URL contiene
   `/api/tiles/`:

```javascript
performance.getEntriesByType("resource")
    .map(x => x.name)
    .filter(x => x.includes("/api/tiles/"))
```

Resultado esperado:

- Cada toggle oculta y vuelve a mostrar solamente su capa.
- Los rellenos permanecen debajo de límites y vías; las etiquetas quedan al
  final del orden visual.
- En zoom 16 aparecen solicitudes para las capas de detalle.
- No hay solicitudes a nombres de capa distintos de la lista blanca.
- La vista inicial queda centrada dentro del bbox de Uyuni, con zoom no mayor
  a 14 y mayor que 10, suficiente para solicitar las capas de escala urbana.
- La traza `encuadre aplicado` registra `w > 0` y `h > 0`; no aparece el WARN
  `encuadre municipal inválido: zoom <= 1`.
- El valor `h` ocupa más del 70% de `window.innerHeight` y el borde inferior del
  lienzo coincide visualmente con el borde inferior del viewport, sin una zona
  en blanco posterior al mapa.
- La traza demuestra que el catálogo serializado no está vacío y que el camino
  de inicialización aplicó el encuadre y llegó a `addSource` para las siete
  capas.
- Cada `urlTemplate` empieza con el origen real abierto en el navegador y
  termina en `/api/tiles/<capa>/{z}/{x}/{y}.mvt`; ninguna contiene un host
  literal de desarrollo.
- Las trazas `transformRequest` aparecen antes de las solicitudes de tiles y no
  muestran el valor del token. No aparece `error MapLibre`.

Criterio de fallo: falta un toggle, un toggle afecta otra capa, parcelas o
edificaciones se solicitan a zoom bajo, falta una traza de inicialización,
`cantidadCapas`/`fuentes` no vale 7, falta la traza `encuadre aplicado`, el
centro queda fuera de Uyuni, `w`/`h` no son positivos, el zoom es menor o igual
que 10, aparece el WARN de encuadre inválido, no hay solicitudes `/api/tiles/`,
la plantilla es relativa o pierde placeholders, no aparece `transformRequest`
para recursos `Tile`, el alto del mapa es menor o igual al 70% del viewport,
queda un corte horizontal con espacio en blanco, o hay errores MapLibre en
consola.

## 7. Capturar tile 200 y sus encabezados

1. En Network filtrar por `.mvt`.
2. En zoom 16 seleccionar un tile central con estado 200. Se recomienda
   `parcelas` o `edificaciones`.
3. Capturar URL, estado y Response Headers, ocultando por completo el valor de
   `Authorization`.

Resultado esperado:

```text
Status: 200
Content-Type: application/vnd.mapbox-vector-tile
ETag: "<sha256>"
Cache-Control: private, no-cache
Vary: Authorization
```

Un 204 aislado es válido para un tile sin features, pero no sustituye la
evidencia 200 requerida en este paso.

Criterio de fallo: falta de ETag, Content-Type incorrecto, 401 con sesión
vigente, tile vacío en el centro de una capa con datos o exposición del token
en la captura.

## 8. Verificar revalidación 304 con la misma sesión

1. Mantener **Preserve log** activado y la caché habilitada.
2. Anotar una URL `.mvt` que haya respondido 200.
3. Limpiar solamente la lista visual de Network; no limpiar datos del sitio ni
   caché.
4. Pulsar el enlace **SG Catastro** de la barra superior. Es navegación interna
   Blazor: `/` detecta la sesión vigente y regresa a `/visor`, recreando el mapa
   con el mismo Bearer token.
5. Volver al mismo zoom y encuadre si la cámara no quedó restaurada.
6. Localizar la misma URL anotada.

Resultado esperado:

- La solicitud repetida incluye `If-None-Match` con el ETag anterior.
- La respuesta es 304 y no transfiere nuevamente el cuerpo MVT.
- No se solicita un nuevo login y el token no cambia durante la navegación.

Criterio de fallo: nunca aparece 304 después de repetir la misma URL con la
misma sesión, falta `If-None-Match`, se pierde la sesión o se descarga de nuevo
un 200 sin una causa documentada de caché del navegador.

## 9. Verificar consolidación de 401 y re-login manual

1. Iniciar una sesión nueva y dejar la pestaña abierta durante al menos 16
   minutos, sin recargarla. El access token expira a los 15 minutos.
2. Con Network filtrado por `.mvt`, hacer un paneo y un cambio de zoom que
   soliciten varios tiles a la vez.
3. Contar las respuestas 401 y los avisos visibles de la aplicación.

Resultado esperado:

- Pueden aparecer varios tiles 401 de una misma ráfaga.
- La aplicación procesa la ráfaga una sola vez, destruye el mapa y navega al
  login.
- Se muestra una sola advertencia: `La sesión expiró. Inicie sesión nuevamente
  para continuar cargando el mapa.`
- No se llama automáticamente a `/api/auth/refresh`.
- Un nuevo login manual recupera el visor y conserva visibilidad/cámara cuando
  sigue siendo aplicable.

Criterio de fallo: múltiples avisos o redirecciones, bucle de 401, refresh
automático, mapa operando con token vencido o imposibilidad de reingresar.

## 10. Evidencia mínima y criterio de cierre

Conservar:

1. Salida de `docker compose ps`, cuatro health `healthy` y Caddy `running`.
2. Runtime Blazor fingerprinted con Content-Type JavaScript y no HTML.
3. Login 200 sin payload ni tokens visibles.
4. Mapa con los siete toggles y geometrías visibles en zoom 16.
5. Tile 200 con `Content-Type`, `ETag`, `Cache-Control` y `Vary`.
6. Misma URL de tile revalidada con 304.
7. Ráfaga de 401 y un único aviso de sesión expirada.

T-2.2 falla si falta cualquiera de esas siete evidencias, si aparece un secreto
en una captura o si consola/red registra un error no explicado.
