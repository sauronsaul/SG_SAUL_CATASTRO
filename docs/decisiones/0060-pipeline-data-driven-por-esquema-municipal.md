# ADR 0060 - Pipeline de importación dirigido por esquema municipal

**Fecha**: 2026-07-16
**Estado**: Aceptado
**Relación**: Fase 3.A.2b; continúa ADR 0059.

## Contexto

ADR 0059 incorporó el catálogo de municipios y los esquemas de capas por
municipio, pero los consumidores del pipeline todavía conservaban supuestos de
Uyuni: el request no declaraba municipio, el ZIP exigía siete nombres fijos, la
carga recorría tipos codificados y el preview ejecutaba validaciones prediales
aunque el esquema no incluyera parcelas.

Caranavi (`022001`) inicia con tres capas: manzanas, áreas urbanas y puntos
geodésicos. El pipeline debe aceptar ese paquete sin inventar parcelas ni
aplicar reglas que solo tienen sentido cuando existe `TipoCapa.Predios`.

El análisis I5 de `ActivacionVersionServicio` en el estado anterior a esta
decisión (commit `3096d9e`) mostró un riesgo destructivo. El método construía
`tripletesVersion` al recorrer las parcelas cargadas y luego llamaba
`MarcarAusenteEnDataset` para cada predio del maestro municipal cuya tripleta
no estuviera en ese conjunto. En una versión válida sin parcelas, el conjunto
quedaría vacío y todo el maestro municipal sería marcado como ausente. Este
hallazgo justifica que la reconciliación no se ejecute cuando el esquema no
contiene parcelas; no se interpreta la ausencia de la capa como un dataset
predial vacío.

## Decisión

### Municipio explícito y validación previa

`POST /api/importaciones/versiones` exige el campo multipart
`municipio_codigo`. Antes de almacenar el ZIP o encolar trabajo, la aplicación:

1. valida el formato INE de seis dígitos;
2. comprueba que el municipio exista en `dominio.municipios`;
3. obtiene su esquema desde `dominio.esquemas_capas` y rechaza una
   configuración vacía o inconsistente; y
4. inspecciona el ZIP contra los archivos y perfiles definidos por ese
   esquema.

Una capa obligatoria ausente invalida el paquete. Una capa opcional puede
omitirse por completo, pero si aparece debe incluir el conjunto coherente de
archivos SHP, DBF, SHX y PRJ. La inspección no depende de una lista global de
nombres de Uyuni.

### Carga y preview dirigidos por datos

La carga reinspecciona el objeto recuperado de MinIO y procesa únicamente las
capas presentes que estén declaradas por el esquema municipal. El tipo de capa,
perfil, nombre de archivo y tabla destino se obtienen de la configuración
persistida. Caranavi incorpora perfiles seed para sus tres capas y conserva los
atributos de origen no mapeados en `atributos_extra`.

El preview registra el esquema evaluado y aplica cada control solo cuando
corresponde:

- B1, B2 y B4 se evalúan únicamente si existe la capa Predios.
- B3 comprueba las capas obligatorias definidas por el esquema municipal.
- O1 y O4 recorren las capas realmente presentes, sin una lista fija de
  tablas.
- La proyección de reconciliación queda expresamente marcada como omitida si
  no existe Predios.

La activación mantiene una sola versión Activa por municipio y permite que
municipios distintos tengan versiones activas simultáneamente. Sin Predios no
consulta ni modifica el maestro predial, y el resumen de reconciliación declara
la omisión y su motivo.

### Nomenclatura de observaciones

- **O1** identifica geometrías no nulas que PostGIS considera inválidas. El
  código pasa a formar parte explícita del contrato de preview y su evaluación
  se generaliza a las capas presentes.
- **O4** identifica geometría nula real. Su emisión se generaliza mediante el
  esquema y las tablas presentes, incluida la identificación desde columnas
  tipadas o `atributos_extra`.
- **O2 y O3** aparecen en documentación histórica de las fases 1 y 2, pero
  nunca tuvieron implementación ni definición en código. Quedan reservados,
  sin definición vigente y sin emisión. Cualquier observación futura que use
  uno de esos códigos requerirá un ADR propio que defina su semántica y
  contrato.

Esta nomenclatura evita atribuir retroactivamente significado a códigos que el
sistema nunca implementó.

## Consecuencias

- El mismo endpoint admite paquetes municipales distintos mediante catálogo y
  configuración, sin bifurcar el pipeline por municipio.
- Agregar un esquema compatible no requiere modificar las listas de capas del
  inspector, la carga ni O4.
- Un paquete sin parcelas no altera el maestro predial y deja evidencia
  explícita de la reconciliación omitida.
- Los errores de municipio inexistente, esquema ausente y paquete incompatible
  se detectan antes de MinIO y de la cola.
- La configuración municipal incoherente falla cerrada.
- No se crea M016: los perfiles de Caranavi se incorporan por el seeder sobre
  las estructuras introducidas por M015.
