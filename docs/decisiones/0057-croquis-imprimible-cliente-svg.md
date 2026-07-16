# ADR 0057 - Croquis imprimible en cliente con geometría planar SVG

**Fecha**: 2026-07-15
**Estado**: Aceptado
**Relación**: implementa T-2.5; complementa los ADR 0055 y 0056; deja una
condición explícita para el diseño del certificado de Fase 4.

## Contexto

La ficha predial necesita producir un croquis simple que los técnicos del GAM
puedan imprimir desde el visor. Es una pieza informativa y reutilizable como
base visual futura, no un certificado: no lleva folio, firma, QR ni registro de
emisión. El contrato anterior sólo incluía un `bbox` en EPSG:4326, suficiente
para encuadrar MapLibre pero insuficiente para reconstruir el contorno del
predio de forma determinista.

El certificado de Fase 4 probablemente requerirá generación y custodia de PDF
en servidor. Adelantar ese mecanismo en T-2.5 introduciría responsabilidades
documentales y de trazabilidad que este entregable no posee.

## Decisión

- El croquis se genera en Blazor WebAssembly como una vista previa en la misma
  pestaña y se imprime con `window.print()` y CSS `@media print`. Se usa A4
  horizontal y no se incorpora una dependencia nueva.
- `GET /api/predios/buscar` conserva su selección server-side de la versión
  `Activa` y agrega `GeometriaPlanar`. El contrato transporta `srid = 32719`,
  `tipo = "Polygon"` y anillos con posiciones métricas `[este, norte]`.
- El campo no se denomina GeoJSON: RFC 7946 prescribe coordenadas geográficas
  WGS84 y un consumidor futuro no debe interpretar los valores UTM como
  longitud y latitud. El SRID explícito y el nombre planar eliminan esa
  ambigüedad.
- El SVG mantiene norte de cuadrícula arriba, conserva proporciones y usa
  `fill-rule="evenodd"` para admitir anillos interiores.
- La barra de escala gráfica vive dentro del SVG. Su segmento se calcula en
  metros mediante el mismo factor `unidades SVG por metro` usado para proyectar
  el polígono. Por ello polígono, barra y rótulo sobreviven juntos a cualquier
  reescalado de impresión. La referencia nominal `1:N`, calculada para el ancho
  esperado en A4 al 100 %, es secundaria; prevalece la barra gráfica.
- La flecha `N - UTM 19S` representa norte de cuadrícula, coherente con las
  coordenadas EPSG:32719. No se rota artificialmente hacia norte geográfico.
- La fecha se obtiene desde UTC y se convierte explícitamente al offset fijo de
  Bolivia `UTC-4`; el croquis muestra `dd/MM/yyyy HH:mm UTC-4`.
- El membrete es textual porque el repositorio no contiene un logotipo oficial
  aprobado. Se muestran triplete, códigos, propietario de referencia,
  ubicación, uso, estado, superficies, versión interna del dataset y la
  advertencia de que no constituye certificado oficial.

## Convergencia de meridianos en Uyuni

La convergencia se midió sobre el centro de la extensión de las parcelas de la
versión activa Uyuni v3. Se comparó el azimut geodésico de un segmento de
`1.000 m` orientado al norte de cuadrícula en EPSG:32719:

```sql
WITH activa AS (
    SELECT id, numero_version
    FROM dominio.dataset_versiones
    WHERE municipio_codigo = 'UYUNI' AND estado = 'Activa'
), centro AS (
    SELECT a.numero_version,
           ST_SetSRID(ST_Centroid(ST_Extent(cp.geometria)::geometry), 32719) AS p
    FROM activa a
    JOIN dominio.capa_parcelas cp ON cp.dataset_version_id = a.id
    GROUP BY a.numero_version
), medida AS (
    SELECT numero_version, p,
           degrees(ST_Azimuth(
               ST_Transform(p, 4326)::geography,
               ST_Transform(ST_Translate(p, 0, 1000), 4326)::geography)) AS azimut
    FROM centro
), normalizada AS (
    SELECT numero_version, p, azimut,
           CASE WHEN azimut > 180 THEN azimut - 360 ELSE azimut END AS convergencia
    FROM medida
)
SELECT numero_version,
       round(ST_X(p)::numeric, 3) AS este_centro_m,
       round(ST_Y(p)::numeric, 3) AS norte_centro_m,
       round(azimut::numeric, 8) AS azimut_norte_cuadricula_grados,
       round(convergencia::numeric, 8) AS convergencia_grados,
       round((20 * tan(radians(abs(convergencia))))::numeric, 4)
           AS desviacion_lateral_en_20m
FROM normalizada;
```

Resultado crudo:

| versión interna | centro E | centro N | azimut norte cuadrícula | convergencia | desviación lateral en 20 m |
|---:|---:|---:|---:|---:|---:|
| 3 | 726836,333 m | 7735942,488 m | 359,23967358° | -0,76032642° | 0,2654 m |

La magnitud no se clasifica como despreciable de forma general. En una
dimensión típica de predio de `20 m` equivale a unos `26,5 cm` de desviación
aparente: queda por debajo de `30 cm` y es irrelevante para la lectura de este
croquis informativo a escala de papel. No es despreciable para un plano o
documento con pretensión topográfica.

Fase 4 debe heredar expresamente esta restricción al diseñar el certificado. Si
el certificado pretende exactitud topográfica deberá definir tolerancia,
referencia de norte y si calcula convergencia por predio; no puede reutilizar la
flecha del croquis como afirmación automática de norte geográfico.

## Alternativas descartadas

### Captura del mapa MapLibre

Depende de tiles cargados, capas visibles, temporización del canvas y resolución
de pantalla. Puede incorporar cartografía no deseada y no ofrece una escala
métrica determinista.

### PDF generado por servidor

Es adecuado para el certificado oficial, junto con folio, firma, QR y registro
inmutable. Para el croquis simple añade complejidad y adelanta Fase 4 sin una
necesidad presente.

## Consecuencias

- El contrato de ficha crece de forma aditiva y explícita con una geometría
  Polygon EPSG:32719.
- El croquis no depende de MapLibre ni del estado visual del mapa.
- La barra gráfica es comprobable matemáticamente y permanece como referencia
  métrica primaria al cambiar la escala física de impresión.
- El diálogo nativo y la salida física o PDF requieren juicio humano; el E2E
  verifica que la vista se abre con los datos, el SVG, la barra, el norte y la
  fecha, pero no declara por sí solo que una impresora concreta produce una
  hoja legible.
- La nota de convergencia queda resuelta para este alcance y se convierte en
  entrada obligatoria del diseño de Fase 4.
