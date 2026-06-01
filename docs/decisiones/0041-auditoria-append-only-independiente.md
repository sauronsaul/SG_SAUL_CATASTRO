# ADR 0041 — Auditoría append-only e independiente del dominio

**Estado**: Aceptado
**Fecha**: 2026-06-01
**Sprint**: 3 (decisión retrospectiva, prueba P7 del Punto de Control 3.2)

---

## Contexto

El módulo de auditoría (`auditoria.auditoria`) y el módulo de dominio
catastral (`dominio.predios`, `dominio.construcciones`, etc.) viven en
**schemas PostgreSQL separados**. La tabla `auditoria.auditoria` no
tiene foreign keys hacia las tablas de dominio: el campo `entidad_id`
es un `string` que guarda el UUID de la entidad auditada, sin
restricción referencial.

Esta característica no estaba documentada explícitamente como decisión
de diseño. Fue descubierta durante la prueba P7 del Punto de Control
3.2 (Sprint 3), al analizar los conteos de auditoría:

- 24.053 registros de INSERT de `Predio` en la tabla `auditoria`.
- Solo 11.985 predios vivos en `dominio.predios`.
- Diferencia: 12.068 registros de auditoría que referencian predios
  ya inexistentes (lote A de la primera importación + 83 registros
  de desarrollo manual).

El origen de la divergencia: el schema `dominio` fue reseteado entre
sesiones de desarrollo (los predios del lote A dejaron de existir);
el schema `auditoria` no fue tocado y sus registros sobrevivieron al
borrado de las entidades referenciadas.

## Decisión

Se formaliza como invariante del sistema que **la auditoría es
append-only e independiente del ciclo de vida del dominio**.

1. **Sin FK hacia el dominio.** `auditoria.auditoria.entidad_id` es y
   permanecerá un string sin foreign key. Los borrados (físicos o
   lógicos) en el dominio NO se propagan a la auditoría.

2. **Sin operaciones de borrado.** La tabla `auditoria` recibe
   solamente INSERTs. No existen UPDATE ni DELETE legítimos sobre
   sus filas. El interceptor solo escribe; ningún caso de uso del
   dominio modifica registros existentes.

3. **Registros huérfanos son intencionales, no basura.** Un registro
   de auditoría de una entidad que existió y dejó de existir es
   **información legítima**, no error a limpiar. Es exactamente lo
   que permite responder preguntas como "¿qué pasó con el predio X
   que ya no está?".

## Consecuencias

**Positivas:**

- La auditoría cumple su función: un log que se borra con el dato
  auditado no sirve como log.
- El schema `dominio` puede modificarse, migrarse o resetearse sin
  romper la integridad referencial de la auditoría.
- Cumple expectativas regulatorias estándar para sistemas catastrales
  municipales (la trazabilidad histórica es el producto del sistema).

**Negativas / costos aceptados:**

- La tabla `auditoria` crece monótonamente. Para el piloto Uyuni el
  volumen es manejable (~128k registros tras importación completa).
  Para municipios grandes (>50k predios con múltiples importaciones
  anuales), habrá que considerar particionamiento por fecha o
  archivado frío de registros antiguos. Deuda registrada para
  Sprint 5+.

- Consultas que crucen `auditoria.entidad_id` con `dominio.predios.id`
  para obtener "estado actual del predio" requieren JOIN explícito y
  pueden devolver `NULL` del lado dominio (registros huérfanos
  legítimos). Documentar este caso en cualquier reporte que cruce
  los dos schemas.

**Restricción de operación:**

- Cualquier procedimiento de mantenimiento que considere "limpiar
  huérfanos de auditoría" debe ser rechazado por revisión. No es
  basura: es historia.

## Alternativas descartadas

| Alternativa | Motivo de descarte |
|---|---|
| FK con `ON DELETE CASCADE` desde auditoría hacia dominio | Eliminaría el log al eliminar el dato auditado — anula el propósito de auditar |
| FK con `ON DELETE RESTRICT` | Impediría borrar predios físicamente; rompe el patrón de soft-delete del proyecto |
| Limpieza periódica de huérfanos | Pérdida de información histórica con beneficio nulo |
| Mover auditoría a base de datos separada (write-only) | Complejidad operativa (segundo motor a respaldar, monitorear) sin beneficio en MVP local |

## Verificación

P7 del Punto de Control 3.2 ejecutó el conteo cruzado:

- `SELECT COUNT(DISTINCT entidad_id)` en `Insert` de `Predio`: 24.053
- `SELECT COUNT(*)` en `dominio.predios`: 11.985
- Diferencia: 12.068 huérfanos = 11.985 (lote A previo al reset) + 83
  (desarrollo manual)

Cero duplicados de `entidad_id` dentro del mismo lote — el interceptor
no dispara dos veces por el mismo `SaveChanges`. La aritmética del
lote A + manual cierra exactamente.

## Deuda registrada

- **Sprint 5+** (escala municipal grande): definir estrategia de
  particionamiento o archivado de `auditoria.auditoria` cuando el
  volumen supere umbrales operativos. Posibles enfoques: partición
  nativa de PostgreSQL por mes, o exportación a almacenamiento frío
  con catalogación.

- **Inmediato (documentación operativa)**: incluir esta invariante en
  el manual de operación del sistema, para que ningún administrador
  futuro intente "limpiar" la tabla sin entender la consecuencia.

## Referencias

- ADR 0011 — Convención de nombres de módulo en auditoría
- ADR 0025 — Soft-delete de UsuarioIdentidad y revocación de
  RefreshTokens (patrón análogo de "no perder información histórica")
- ADR 0038 — Auditoría correcta de entidades OwnsOne (decisión técnica
  del mismo módulo)
- Hallazgo P7 del Punto de Control 3.2 (Sprint 3)
