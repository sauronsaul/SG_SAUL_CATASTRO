# ADR 0062 — Visor multimunicipio data-driven y hot-swap

**Fecha**: 2026-07-17
**Estado**: Aceptado
**Relación**: implementa 3.A.2c; continúa ADR 0055, ADR 0057, ADR 0059 y ADR 0060.

## Contexto

El visor estaba fijado a Uyuni mediante `TilesSettings`, `ConfiguracionVisor`
y un catálogo duplicado de siete capas (`CapaTile`/`CatalogoCapasTile`). Esa
estructura no representaba el esquema real de Caranavi, que incorpora áreas
urbanas y puntos geodésicos, ni capacidades todavía ausentes, como la búsqueda
predial.

Desde M015, `dominio.municipios`, `dominio.esquemas_capas` y
`dominio.dataset_versiones` contienen la identidad municipal, las capas
admitidas y la versión activa. Mantener otra configuración fija producía una
segunda fuente de verdad.

La sesión Blazor conserva el access token sólo en memoria. Una recarga completa
para cambiar de municipio lo elimina y obliga a autenticar de nuevo. Por eso el
cambio debe ocurrir dentro de la misma aplicación y sesión.

## Decisión

### Contrato municipal

Se exponen dos endpoints con la misma política de autorización de tiles,
`Admin,Tecnico`:

- `GET /api/visor/municipios`: municipios que tienen una versión `Activa`;
- `GET /api/visor/{municipio}/configuracion`: identidad municipal, versión
  activa, bbox WGS84, capas presentables ordenadas y capacidades.

La configuración devuelve `400` para geocódigo malformado, `404` para municipio
inexistente, `409` cuando no hay versión activa o esquema configurado y `422`
cuando la versión activa no contiene geometrías. Ninguno queda anónimo.

El bbox agrega primero `ST_Extent` en EPSG:32719 y transforma sólo la caja
resultante a EPSG:4326. La verificación mantuvo los rangos esperados:

```text
022001: oeste=-67.58575329 sur=-15.85119123 este=-67.53925450 norte=-15.82111894
051201: oeste=-66.84783997 sur=-20.48334803 este=-66.80317594 norte=-20.44048490
```

### Fuentes únicas de capas

Se eliminan `CapaTile`, `CatalogoCapasTile` y `TilesSettings`.
`TipoCapa` y `dominio.esquemas_capas` determinan qué capas existen. Un único
catálogo por `TipoCapa` aporta ruta, título, orden, zoom, color y representación.
Una capa sólo se sirve si pertenece al catálogo y al esquema municipal.

Los tiles pasan a `GET
/api/tiles/{municipio}/{capa}/{z}/{x}/{y}.mvt`. El SQL permanece estático por
`TipoCapa`; ningún identificador de tabla proviene de la URL. El ETag incorpora
municipio, id y número de versión, capa y coordenadas. La ruta devuelve `400`
para municipio o coordenadas malformadas, `404` para municipio/capa inexistente
o fuera del esquema, `409` sin dataset activo, `204` para tile vacío, `304`
para ETag coincidente y `200` para MVT disponible.

La búsqueda pasa a `GET
/api/predios/{municipio}/buscar?distrito=...&manzana=...&predio=...`.

### Hot-swap del frontend

Se elige Uyuni (`051201`) si está disponible y, si no, el primer municipio
activo. Al cambiar, la aplicación obtiene la configuración con el token
vigente, destruye el mapa anterior, limpia cámara/visibilidad/ficha/resaltado y
crea el mapa nuevo. No recarga el documento porque eso perdería el token en
memoria. Sin capa `Predios`, no registra interacción de parcelas y reemplaza el
buscador por una explicación explícita. Ficha y croquis usan el catálogo
municipal, no textos fijos.

### Defensa del esquema de importación

Aunque `tabla_destino` no proviene del cliente, O1 y O4 validan el identificador
contra `^[a-z_]+$` antes de interpolarlo en SQL.

## Consecuencias

- El cambio de municipio conserva la sesión.
- Mapa, ficha y croquis resuelven la versión activa del municipio explícito.
- Añadir una capa soportada no duplica catálogos entre API y frontend.
- Caranavi muestra cartografía sin inventar búsqueda predial.
- La transformación del bbox es constante respecto del número de geometrías.
- No se agregan dependencias ni migraciones.
