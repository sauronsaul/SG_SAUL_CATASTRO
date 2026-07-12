# ADR 0051 — Preview completo, activación atómica y reconciliación del maestro

**Fecha**: 2026-07-12  
**Estado**: Aceptado

## Contexto

Las siete capas versionadas ya se cargan de forma asíncrona, pero publicar una
versión exige validar la entrega y proyectar sus parcelas sobre el registro
maestro sin romper documentos, historial ni otros datos institucionales.

## Decisiones

1. El reporte completo de preview extiende `reporte_preliminar` JSONB. Conserva
   los conteos de carga y agrega bloqueantes B1–B4, geometrías inválidas,
   diferencias contra la activa y proyección de altas/ausencias. El reporte se
   persiste antes de publicar `PreviewListo`, en el mismo `SaveChanges`.
2. El resumen de reconciliación se persiste por separado en
   `resumen_reconciliacion` JSONB, agregado por M012.
3. `POST /api/importaciones/versiones/{id}/activar` se restringe a `Admin` y
   ejecuta en aislamiento `Serializable`. Archivar la activa actual, activar o
   reactivar el destino, reconciliar y guardar el resumen forman una sola
   transacción.
4. La reactivación usa `ReactivarDesdeArchivada`, una transición explícita del
   agregado. No existe un camino alternativo de rollback.
5. La reconciliación opera en memoria por triplete y usa métodos de `Predio`:
   alta, actualización de atributos importados, reaparición y ausencia. Nunca
   borra ni hace soft-delete. Un predio sin cambios materiales no genera UPDATE
   ni auditoría; `ultima_version_vista_id` conserva en ese caso la última
   versión que modificó materialmente su proyección.
6. `GeometriaPredial.CrearDesdeImportacion` mantiene Polygon y SRID 32719 como
   invariantes, pero tolera invalidez topológica. Toda geometría inválida que
   entra al maestro establece `requiere_revision=true` y agrega el motivo de
   `ST_IsValidReason` sin borrar revisiones anteriores.
7. Superficie declarada nula o no positiva es bloqueante B4. La reconciliación
   conserva una guarda equivalente como defensa final.
8. El endpoint legado `POST /api/importaciones/{id}/confirmar` devuelve 410.
   El preview legado y su handler permanecen disponibles; el handler de
   confirmación se conserva sin exposición HTTP para una limpieza posterior.

## Auditoría e inmutabilidad

La reconciliación usa el `ApplicationDbContext` normal con sus interceptores.
No modifica filas de `capa_*`; los triggers de inmutabilidad permanecen sin
cambios. Solo la carga masiva de capas utiliza el contexto dedicado sin
interceptores ya establecido en el ADR 0050.

## Consecuencias

- Una versión con bloqueantes permanece `PreviewListo` y la activación retorna
  un error 422 con los códigos encontrados.
- Una ausencia conserva el `predios.id`, historial, documentos y relaciones;
  además deja revisión humana pendiente.
- La reaparición restaura presencia, pero nunca limpia automáticamente una
  revisión previa.
- El índice parcial de una activa por municipio sigue siendo la última defensa
  ante activaciones concurrentes.
