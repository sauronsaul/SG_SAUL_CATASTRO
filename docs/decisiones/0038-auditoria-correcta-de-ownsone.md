# ADR 0038 — Auditoría correcta de entidades OwnsOne

**Estado**: Aceptado
**Fecha**: 2026-05-27
**Sprint**: 3
**Commit que aplica la decisión**: 3e24d67

---

## Contexto

El `AuditoriaInterceptor` de EF Core captura cambios en las entidades del
dominio y los serializa a la tabla `auditoria.auditoria`. Hasta el commit
`3e24d67` el interceptor trataba cada `EntityEntry` como una entrada
independiente, sin distinguir entre entidades raíz y entidades poseídas
(`OwnsOne` de EF Core).

Esto causaba dos problemas concretos en el módulo de catastro:

1. **`UbicacionCatastral`** (OwnsOne del `Predio`, mapeado inline en la
   misma tabla `dominio.predios`) generaba un registro de auditoría
   propio en cada INSERT/UPDATE de un predio, además del registro del
   predio padre. En la importación de Uyuni (~12.000 predios) esto
   producía ~12.000 registros de auditoría adicionales — verificado
   empíricamente en P7 del Punto de Control 3.2 (12.068 registros
   standalone de `UbicacionCatastral` en la BD).

2. **Cambios que afectaban solo a propiedades del OwnsOne** dejaban al
   `EntityEntry` del padre en estado `Unchanged`, por lo que el
   interceptor no auditaba nada — el cambio quedaba silenciosamente
   sin registro. Caso real: técnico modifica `Predio.Ubicacion.Zona`
   sin tocar ninguna columna escalar del predio.

Una versión previa (T8) había excluido `GeometriaPredial` puntualmente
del registro de auditoría, pero por *nombre de propiedad* y solo para
ese owned específico. La solución no era estructural.

## Decisión

El interceptor adopta tres reglas que tratan los OwnsOne como parte
integral del agregado padre:

1. **Saltar `entry.Metadata.IsOwned()` genérico.** Cualquier
   `EntityEntry` cuyo metadata indique que es una entidad poseída se
   descarta como entrada independiente del registro de auditoría.
   Cubre `UbicacionCatastral`, `GeometriaPredial` y cualquier OwnsOne
   futuro sin necesidad de mantener listas por nombre.

2. **Detectar cambios en owned con padre `Unchanged`.** Para cada
   `EntityEntry` que sea raíz agregada, el interceptor inspecciona sus
   `References` y consulta si algún `TargetEntry` correspondiente a un
   owned está en estado `Added`/`Modified`/`Deleted`. Si lo está, el
   padre se trata como `Modified` aunque su propio estado sea
   `Unchanged`.

3. **Fusionar las propiedades del owned en el diccionario del padre.**
   Al serializar `ValorAnterior` y `ValorNuevo`, las propiedades de los
   OwnsOne (excluyendo PK shadow / FK al padre) se concatenan a las
   propiedades del padre en una sola lista plana. El JSON resultante
   contiene los campos del owned al mismo nivel que los del padre.

`Poligono` permanece en `PropiedadesExcluidas` como defensa en
profundidad: aunque la regla 1 ya excluye `GeometriaPredial` entero, la
exclusión por nombre garantiza que ningún cambio de configuración del
modelo (por ejemplo, mover `Poligono` a un componente no-owned en el
futuro) reintroduzca el blob geométrico en la auditoría sin que nadie
se entere.

## Consecuencias

**Positivas:**

- La auditoría del predio refleja el estado completo del agregado
  (predio + ubicación) en un único registro JSON, no fragmentado en
  dos entradas que un revisor debería correlacionar.
- Cambios en propiedades del owned con padre `Unchanged` ya no se
  pierden — son auditados como UPDATE del padre.
- La regla 1 es genérica: cualquier OwnsOne futuro queda cubierto
  sin tocar el interceptor.

**Negativas:**

- El JSON de `ValorAnterior` / `ValorNuevo` mezcla campos del padre
  con campos del owned al mismo nivel. Un consumidor que asuma "una
  propiedad = una columna del padre" debe ser actualizado para
  contemplar campos como `Zona` o `Manzana` que provienen del owned.

**Restricciones reconocidas:**

- La regla 1 asume que ningún OwnsOne del dominio amerita auditarse
  como entidad independiente. Si en el futuro se agrega un OwnsOne
  con identidad propia (caso poco común en DDD), habrá que revisar
  esta regla.

## Alternativas descartadas

| Alternativa | Motivo de descarte |
|---|---|
| Mantener exclusión por nombre de propiedad (patrón previo de T8) | No escala: cada OwnsOne nuevo requiere actualizar `PropiedadesExcluidas` o el código del interceptor, y se vio en P7 que `UbicacionCatastral` no había sido cubierto |
| Auditar el OwnsOne como entidad propia con tabla separada en la tabla auditoria | Duplica información: el OwnsOne ya vive en la misma tabla de BD que el padre; auditarlo aparte rompe la unidad del agregado |
| Eliminar el AuditoriaInterceptor y auditar manualmente desde cada handler | Sobrecarga de código en cada caso de uso, pierde la garantía de uniformidad |

## Migración de datos

Los ~12.068 registros standalone de `UbicacionCatastral` ya existentes
en la tabla (generados por la primera importación de Uyuni, anterior a
este fix) **no se eliminan**. Son evidencia histórica del problema
previo y, en coherencia con el principio de auditoría append-only
(ver ADR 0041), no se purgan. Las importaciones posteriores al fix ya
no los generan — verificado en P7.

## Verificación

Diagnóstico ejecutado contra BD real con tres casos:

1. INSERT de predio: un único registro de auditoría con propiedades del
   padre + propiedades de `UbicacionCatastral` fusionadas.
2. UPDATE de propiedad escalar del predio (sin tocar owned): registro
   con `ValorAnterior` y `ValorNuevo` reflejando el cambio.
3. UPDATE de solo propiedad de `UbicacionCatastral` (padre Unchanged):
   registro generado correctamente, antes del fix se perdía.

No se agregaron tests unitarios del interceptor en el commit `3e24d67`.
**Deuda registrada**: cubrir el interceptor con tests de integración
que validen los tres casos anteriores y prevengan regresiones futuras
del comportamiento OwnsOne. Sprint 4.

## Referencias

- ADR 0011 — Convención de nombres de módulo en auditoría
- ADR 0036 — Estrategia transaccional en ConfirmarImportacionHandler
  (incluye nota sobre exclusión de geometría en auditoría)
- ADR 0041 — Auditoría append-only e independiente del dominio
- Hallazgo P7 del Punto de Control 3.2 (Sprint 3): cuantificación de
  los 12.068 registros huérfanos pre-fix
