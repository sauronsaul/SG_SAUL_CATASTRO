# ADR 0059 - Geocódigo INE como identificador municipal canónico

**Fecha**: 2026-07-16
**Estado**: Aceptado
**Relación**: Fase 3.A.2a; complementa ADR 0030 y reemplaza el slug municipal
`UYUNI` usado por el modelo versionado de Fase 2.

## Contexto

El modelo versionado identificaba a Uyuni mediante el slug `UYUNI`. El valor
solo existía en `dataset_versiones`, mientras el maestro `predios` no tenía
dimensión municipal y su tripleta era única globalmente. La reconciliación
cargaba todos los predios e indexaba exclusivamente por
`(cod_uv, cod_man, cod_pred)`.

Ese diseño no permitía incorporar otro municipio con tripletas coincidentes.
Tampoco expresaba que cada municipio puede entregar un conjunto distinto de
capas: Uyuni tiene siete capas versionadas y el primer paquete de Caranavi
tiene manzanas, áreas urbanas y puntos geodésicos, sin parcelas.

La verificación documental cerrada el 16 de julio de 2026 confirma los
geocódigos `051201` para Uyuni y `022001` para Caranavi en publicaciones
oficiales del INE: visor PDF por código y serie Atlas Municipal nombrada por
geocódigo.

## Decisión

### Identificador municipal

Se adopta el geocódigo INE DDPPMM de seis dígitos ASCII como identificador
municipal canónico del dominio:

- Uyuni: `051201`.
- Caranavi: `022001`.

La tabla `dominio.municipios` conserva el código, nombres institucionales,
departamento y fuente documental. `codigo_ine` es llave de negocio única,
tiene CHECK de seis dígitos y recibe las FK de las entidades municipales.

La fuente registrada para ambos seeds es:

> INE Bolivia — Clasificación de Ubicación Geográfica (geocódigo DDPPMM).
> Corroborado en publicaciones oficiales INE (visorPdf Codigo=051201; serie
> AtlasMunicipal nombrada por geocódigo). Verificado 2026-07-16.

La migración M015 reemplaza `UYUNI` por `051201` en las versiones existentes.

### Dimensión municipal del maestro predial

`dominio.predios` incorpora `municipio_codigo` obligatorio y FK al catálogo.
Los predios existentes se asignan a `051201`. La unicidad predial activa pasa
a ser:

```text
(municipio_codigo, cod_uv, cod_man, cod_pred) WHERE NOT is_deleted
```

La reconciliación carga y marca ausencias únicamente dentro del municipio de
la versión. La consulta de ficha une la parcela y el maestro mediante municipio
y tripleta.

### Esquemas de capas por municipio

`dominio.esquemas_capas` registra por municipio el tipo, perfil, archivo SHP,
tabla destino y obligatoriedad. Se siembran siete definiciones para Uyuni y
tres para Caranavi.

Se incorporan `TipoCapa.AreasUrbanas` y `TipoCapa.PuntosGeodesicos`, junto con
`dominio.capa_areas_urbanas` y `dominio.capa_puntos_geodesicos`. Las tablas
siguen el patrón versionado, SRID 32719, índice espacial e inmutabilidad
híbrida por fila y por sentencia.

En 3.A.2a los consumidores actuales obtienen de datos el esquema de Uyuni sin
cambiar su resultado. La selección del municipio desde el request, la carga de
los tipos de Caranavi y la aplicación condicional de validaciones prediales
corresponden a 3.A.2b. El visor y los nombres institucionales corresponden a
3.A.2c.

## Coexistencia con el código catastral provisional

El geocódigo INE y el segmento municipal del código catastral provisional son
sistemas distintos y no se unifican en 3.A.2a:

- `dominio.municipios.codigo_ine = 051201` identifica de forma canónica una
  unidad territorial para aislar datasets, predios y configuración operativa.
- ADR 0030 define provisionalmente el código catastral con segmentos
  `DEP(2)-PROV(3)-MUN(3)-ZONA(3)-MZN(4)-LOTE(4)`. Su segmento municipal sigue
  siendo `028` dentro de `Catastro:CodigoCatastral`.

El primero proviene de la clasificación territorial INE DDPPMM; el segundo
forma parte de una codificación catastral provisional cuya validación
institucional continúa pendiente. Sustituir `028` por `051201` rompería la
longitud y semántica del value object vigente. Mantenerlos separados evita
presentar equivalencia entre identificadores con propósitos diferentes.

## Reversibilidad

El `Down` de M015 se detiene si existen datasets o predios de un municipio
distinto de `051201`. Para el caso simple de Uyuni, revierte
`051201` a `UYUNI` antes de eliminar las FK, columnas y tablas nuevas. No se
intenta colapsar datos multi-municipio a un modelo global.

## Consecuencias

- Tripletas idénticas pueden coexistir entre municipios sin colisión.
- La activación de un municipio no modifica predios de otro.
- Las versiones y los predios quedan referencialmente vinculados a un
  municipio válido.
- El conjunto de capas deja de ser una constante global de Uyuni.
- Las capas de Caranavi existen en el modelo, aunque su carga se habilitará en
  3.A.2b.
- La UI muestra temporalmente el geocódigo; 3.A.2c debe resolver el nombre
  desde el catálogo municipal.
