# ADR 0054 - Tiles MVT on-the-fly versionados

**Fecha**: 2026-07-14  
**Estado**: Aceptado

## Contexto

La Fase 2 requiere entregar cartografia versionada a clientes web sin exponer
consultas SQL ni transferir geometria completa en cada navegacion. Las siete
capas de Uyuni estan en SRID 32719, mientras que el esquema de tiles usa Web
Mercator (SRID 3857). La seleccion de datos vigente debe seguir la version con
`estado = 'Activa'` y no un `numero_version` supuesto.

La version activa contiene geometrías O1 topologicamente invalidas conservadas
como dato real. El camino de tiles no puede repararlas ni excluirlas en
silencio. Tambien debe invalidar el cache completo cuando se active otro
dataset y mantener el acceso limitado a los roles internos durante el MVP.

## Decision

La API genera tiles MVT on-the-fly en PostGIS mediante `ST_AsMVT`,
`ST_AsMVTGeom` y `ST_TileEnvelope`.

- Una lista blanca exacta resuelve los siete nombres logicos a un enum. La
  infraestructura selecciona una de siete sentencias SQL constantes; ningun
  valor de ruta se interpola como identificador SQL.
- Cada sentencia resuelve server-side el `dataset_version_id` activo para el
  municipio configurado, filtra primero en SRID 32719 mediante `&&` y
  `ST_Intersects`, y solo entonces transforma la geometria a 3857.
- Las siete tablas tienen indices GiST parciales sobre `geometria` con
  `WHERE geometria IS NOT NULL`. Se ejecuta `ANALYZE` despues de crear los
  indices.
- Las geometrías O1 no se filtran por validez, no pasan por `ST_MakeValid` y se
  incluyen igual que las validas. Los nulos genuinos O4 no tienen geometria que
  rasterizar ni recuperar.
- El tile solo incluye identificadores del triplete donde existen y los campos
  livianos de nombre/tipo/material necesarios para vias. No se serializa
  `atributos_extra`.
- El ETag fuerte es SHA-256 de
  `(dataset_version_id, capa, z, x, y)`. Una activacion cambia el identificador
  de version y, por tanto, todos los ETag. `If-None-Match` puede responder 304.
- Mientras el endpoint requiera JWT, se usa `Cache-Control: private, no-cache`
  y `Vary: Authorization`. No se habilita cache compartido en Caddy.

## Contrato HTTP

`GET /api/tiles/{capa}/{z}/{x}/{y}.mvt`

- Requiere `[Authorize(Roles = "Admin,Tecnico")]`.
- `capa` admite exactamente `parcelas`, `edificaciones`,
  `predios-no-fotografiados`, `manzanas`, `distritos`, `zonas` y `vias`.
  Cualquier otro valor devuelve 404.
- `z` debe estar en `[0,22]`; `x` e `y` deben estar en
  `[0, 2^z)`. Las coordenadas numericas fuera de rango devuelven 400 antes de
  consultar PostGIS.
- Un tile con features devuelve 200 y
  `Content-Type: application/vnd.mapbox-vector-tile`. Un tile sin features
  devuelve 204. Un ETag coincidente devuelve 304.
- La ruta usa el segmento complejo `{y:int}.mvt`, verificado por una prueba de
  integracion contra el enrutador real de ASP.NET Core.

## Alternativas consideradas

### GeoJSON por ventana

Se descarta como contrato de navegacion principal. Repite nombres de atributos,
produce respuestas mayores y traslada al cliente transformacion y
representacion que MVT resuelve de forma compacta. GeoJSON sigue siendo util
para intercambios o consultas puntuales, no para teselas de mapa.

### Pre-materializacion de tiles

Se posterga. Exige almacenamiento, generacion y purga por version y multiplica
artefactos para siete capas y hasta 23 niveles. La medicion real via
cliente-Caddy-Kestrel-PostGIS dio un maximo de 1,490496 s en 18 solicitudes:
ningun tile supero el umbral de 2 s que justificaria decidir cache agresivo o
pre-materializacion en esta etapa.

## Limitacion consciente

El municipio no forma parte de la ruta. `Tiles:MunicipioCodigo` es una
configuracion explicita y actualmente tiene el valor unico `UYUNI`; no existe
un literal de municipio dentro de las sentencias SQL. La entrada de Caranavi
obligara a decidir si el municipio se deriva del tenant, de claims, del host o
de la ruta. Esa decision es candidata a un nuevo DP y no se adelanta en T-2.1.

## Consecuencias

- Una activacion invalida los ETag sin purgas manuales ni claves basadas en la
  etiqueta comercial de la version.
- La generacion siempre observa una sola version activa y preserva la
  inmutabilidad de las tablas `capa_*`.
- Los indices espaciales quedan disponibles para cualquier consulta por
  ventana sobre las capas versionadas.
- Si una geometria O1 futura hace fallar `ST_AsMVTGeom`, el incidente debe
  reportarse con capa, fila y razon persistida antes de adoptar una exclusion o
  reparacion.
- El cache compartido y la pre-materializacion requieren una decision posterior
  sustentada en nuevas mediciones, no forman parte de este ADR.
