# ADR 0037 — Semántica de los campos de conteos del agregado Importacion

- **Estado:** Propuesto
- **Fecha:** 2026-06-01
- **Sprint origen:** Sprint 3 — Punto de Control 3.2, prueba P8

## Contexto

El agregado `SG.Domain.Importacion.Importacion` persiste cuatro campos de
conteos: `FilasImportadas`, `FilasConAdvertencia`, `FilasRechazadas`,
`FilasOmitidas`. Estos campos eran escritos por un único método
`RegistrarResultados`, invocado desde dos flujos con semántica opuesta:

1. `GenerarPreviewImportacionHandler` — registra una **proyección**:
   los conteos representan "lo que ocurriría si se confirma".
2. `ConfirmarImportacionHandler` — registra el **resultado real** de
   la ejecución contra la base de datos.

Verificado en P8: importaciones en estado `PreviewGenerado` (nunca
confirmadas) tienen `filas_importadas = 11985` y `= 18423` en BD,
cuando semánticamente esos valores son proyecciones, no resultados.
Una consulta `SELECT SUM(filas_importadas) FROM importaciones` sin
filtro por estado cuenta como importadas filas que solo se
previsualizaron.

## Decisión tomada en este commit (parcial — Opción B)

Se separó el método del dominio en dos:
`RegistrarConteosPreview` y `RegistrarConteosConfirmacion`, cada uno
con guarda de estado que solo permite invocación desde
`PreviewGenerado`. Se agregaron guardas equivalentes en `Confirmar()`
y `MarcarFallida()`.

El **modelo de datos no se modificó**: los cuatro campos persistidos
siguen siendo los mismos. La asimetría semántica queda contenida
dentro del dominio.

## Deuda pendiente

1. **Renombrar columnas persistidas** para reflejar la semántica real.
   Propuesta: separar en `filas_estimadas_*` (escritas en preview) y
   `filas_importadas_*` (escritas solo en confirmación). Requiere
   migración EF Core con backfill: importaciones existentes en
   `PreviewGenerado` deberían tener sus valores movidos de
   `filas_importadas` a `filas_estimadas` y poner 0 en
   `filas_importadas`.

2. **Documentar en API**: hasta que (1) se ejecute, los consumidores
   del endpoint `GET /api/importaciones/{id}` y de la tabla
   `importaciones` DEBEN filtrar por `estado = 'Confirmada'` para
   obtener conteos reales. Cualquier reporte que sume
   `filas_importadas` sin ese filtro reporta cifras infladas.

3. **Consecuencia detectada fuera de alcance:** las guardas nuevas
   tiran `DomainException`, mientras que los handlers de aplicación
   usan `Result<T>` con `DomainError`. Hay dos mecanismos paralelos
   para errores de dominio en el proyecto. Verificar que el middleware
   mapea `DomainException` a HTTP 422 o 409 (no 500 genérico).
   Decidir política unificada en ADR separado.

## Origen del hallazgo

Detectado durante P8 del Punto de Control 3.2 del Sprint 3, cuando
un diagnóstico de código concluyó erróneamente que `filas_importadas`
sería 0 en estado `PreviewGenerado` — contradicho por la evidencia
en BD. La causa raíz fue un agregado anémico (`RegistrarResultados`
sin guardas, llamable en cualquier estado) combinado con una
nomenclatura ambigua que invitó al error de lectura.

## Referencias

- Commit que aplica la decisión parcial: 1c40545
