# ADR 0055 - Visor Blazor WebAssembly con MapLibre y tiles versionados

**Fecha**: 2026-07-14
**Estado**: Aceptado
**Relación**: supersede parcialmente al ADR 0046 únicamente en la librería de
mapas; implementa el consumo del contrato MVT definido por ADR 0054.

## Contexto

El ADR 0046 eligió Blazor WebAssembly, estáticos servidos por Caddy, DTOs
compartidos por referencia de proyecto y Leaflet para una demo cartográfica
anterior. Posteriormente, el PLAN_MAESTRO v1.1 prescribió MapLibre y ADR 0054
estableció el endpoint autenticado de tiles vectoriales MVT. Leaflet no
renderiza ese formato de manera nativa y requeriría otra extensión y otro
contrato de interop.

El visor institucional necesita mostrar las siete capas versionadas, evitar
solicitar las capas pesadas a zoom bajo, inyectar el JWT en los tiles y permitir
la revalidación HTTP mediante el ETag asociado a la versión activa.

## Decisión

- El frontend es `SG.Web`, una aplicación Blazor WebAssembly standalone en
  `src/frontend/`, incorporada a `SG.slnx`.
- `SG.Web` referencia `SG.Contracts` mediante `ProjectReference`. La API no
  hospeda Blazor ni se introduce un segundo contrato DTO.
- Se usa MapLibre GL JS 5.24.0 mediante un módulo ES aislado e `IJSRuntime`.
  Sus archivos JS, CSS y licencia se vendorizan con SHA-256 registrado; no hay
  CDN, `package.json`, `node_modules` ni npm en build o runtime.
- Las siete fuentes usan las rutas relativas de ADR 0054. Los rellenos se
  dibujan antes de líneas y etiquetas. Parcelas empiezan en zoom 15;
  edificaciones y predios no fotografiados, en zoom 16.
- `transformRequest` agrega `Authorization: Bearer` sólo a solicitudes
  same-origin cuya ruta comienza con `/api/tiles/`. El token no viaja en la URL.
- La sesión conserva en memoria WASM únicamente access token, expiración y
  usuario. No persiste refresh token, `localStorage` ni `sessionStorage`.
- Ante la primera respuesta 401 de una ráfaga, el módulo consolida eventos,
  conserva cámara/visibilidad, destruye el mapa y exige login manual. No se
  implementa refresh automático en T-2.2.

## Hosting y contrato operativo

SG.Web se publica como archivos estáticos en una imagen multi-stage. Un Caddy
interno los sirve en `http://:3000`; declarar el esquema HTTP explícitamente es
parte del contrato del contenedor. La forma abreviada `:3000` produjo conexiones
abortadas en la sonda real con Caddy 2.11.2, mientras que `http://:3000` entregó
200 para `/`, el fallback `/visor`, `appsettings.json` y MapLibre.

El Caddy exterior conserva el origen único del navegador y usa:

```caddyfile
reverse_proxy {$WEB_UPSTREAM:web:3000}
```

`web:3000` es el valor canónico de Compose. El upstream parametrizado permite
usar `host.docker.internal:5001` con `dotnet watch` sin introducir CORS. Un
despliegue exige rebuild de la imagen web; `docker start` puede revivir código
anterior y no es un mecanismo de despliegue.

Los recursos propios con URL estable (`index.html`, `appsettings.json`,
`css/*`, `js/*` y el fallback SPA) se sirven con `Cache-Control: no-cache` y
ETag. El navegador puede almacenarlos, pero debe revalidarlos y recibe el nuevo
contenido después de un rebuild. Los artefactos con identidad versionada
(`_framework/*` con huella del SDK y MapLibre bajo `5.24.0/`) usan
`public, max-age=31536000, immutable`. Se prefiere esta política a introducir
un pipeline adicional para renombrar módulos propios o una versión manual en
la URL de importación: centraliza el contrato en el servidor estático y cubre
también CSS, configuración y respuestas SPA, no sólo `mapa.js`.

## Configuración mono-municipio

El bbox y `Visor:MunicipioCodigo = UYUNI` viven en
`wwwroot/appsettings.json`, no en código C#. Esta es una limitación consciente
de la misma familia que `Tiles:MunicipioCodigo` de ADR 0054: el municipio no
forma parte de la ruta ni se deriva todavía de tenant, claim o host.

La entrada de Caranavi requiere decidir conjuntamente cómo seleccionan
municipio el endpoint y el visor, y cómo se obtienen bbox/centro por municipio.
Esa decisión es candidata a un DP nuevo y no se adelanta en T-2.2.

## Alternativas consideradas

### Mantener Leaflet

Se descarta sólo para la librería cartográfica. No renderiza MVT nativamente y
obliga a agregar plugins o adaptar el contrato ya cerrado por ADR 0054. Las
demás decisiones de ADR 0046 siguen vigentes.

### Descargar MapLibre desde CDN o incorporar un pipeline npm

Se descarta el CDN por dependencia de internet en entornos municipales. Un
pipeline npm permanente no aporta valor para un único artefacto JS encapsulado;
la distribución fija y verificada conserva un build .NET reproducible.

### Persistir tokens en almacenamiento del navegador

Se descarta `localStorage` por su persistencia y `sessionStorage` porque T-2.2
acepta re-login tras recarga. Mantener el token en memoria reduce su duración y
superficie de exposición.

### Servir Blazor desde SG.Api

Se descarta porque acopla el ciclo de publicación del frontend a la API. Caddy
ya proporciona un origen único y permite imágenes independientes.

## Consecuencias y limitaciones

- Una nueva versión activa cambia el ETag de ADR 0054 sin cambiar URLs ni
  configuración del cliente.
- La vista de ciudad evita descargar parcelas y edificaciones; el detalle se
  solicita sólo al acercarse.
- Una recarga completa pierde la sesión en memoria y requiere login.
- No se usa refresh token automático ni mapa base externo en esta sub-etapa.
- Los tests cubren catálogo, orden/minzoom, sesión, visibilidad y consolidación
  de 401. Los E2E de navegador siguen fuera de T-2.2; la evidencia manual se
  ejecuta literalmente con `docs/operacion/verificacion-visor-t2-2.md`.
