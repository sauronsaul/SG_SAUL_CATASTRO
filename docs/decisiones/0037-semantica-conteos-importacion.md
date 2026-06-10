# ADR 0037 — Semántica de los campos de conteos del agregado Importacion

- **Estado:** Aceptado
- **Fecha de propuesta:** 2026-06-01 (Sprint 3 — PC 3.2, prueba P8)
- **Fecha de resolución:** 2026-06-10 (Sprint 4 — item 2)

---

## Contexto (Sprint 3)

El agregado `SG.Domain.Importacion.Importacion` persistía cuatro campos de
conteos: `FilasImportadas`, `FilasConAdvertencia`, `FilasRechazadas`,
`FilasOmitidas`. Estos campos eran escritos por un único método
`RegistrarResultados`, invocado desde dos flujos con semántica opuesta:

1. `GenerarPreviewImportacionHandler` — registra una **proyección**:
   los conteos representan "lo que ocurriría si se confirma".
2. `ConfirmarImportacionHandler` — registra el **resultado real** de
   la ejecución contra la base de datos.

Verificado en P8: importaciones en estado `PreviewGenerado` (nunca
confirmadas) tenían `filas_importadas = 11985` y `= 18423` en BD,
cuando semánticamente esos valores son proyecciones, no resultados.
Una consulta `SELECT SUM(filas_importadas) FROM importaciones` sin
filtro por estado contaba como importadas filas que solo se
previsualizaron.

## Decisión parcial aplicada en Sprint 3 (commit 1c40545)

Se separó el método del dominio en dos:
`RegistrarConteosPreview` y `RegistrarConteosConfirmacion`, cada uno
con guarda de estado que solo permite invocación desde
`PreviewGenerado`. Se agregaron guardas equivalentes en `Confirmar()`
y `MarcarFallida()`.

El **modelo de datos no se modificó**: los cuatro campos persistidos
seguían siendo los mismos. La asimetría semántica quedó contenida
dentro del dominio pero sin resolver en la capa de persistencia.

---

## Resolución completa (Sprint 4 — item 2, 2026-06-10)

Al diagnosticar el renombrado de `FilasImportadas` se descubrió que los 4
contadores tenían **doble escritura**: tanto `RegistrarConteosPreview`
como `RegistrarConteosConfirmacion` escribían en las mismas propiedades,
de modo que la confirmación sobreescribía los conteos proyectados del
preview sin posibilidad de recuperarlos.

### Opciones evaluadas

**Opción A — Renombrar solo `FilasImportadas`**: dividirla en
`FilasCreadas` + `FilasActualizadas`, mantener las otras 3 sin cambios.
Resuelve la ambigüedad del nombre pero no el problema de doble escritura en los
4 contadores.

**Opción B-advertencia-separada (elegida)** — Separar los 9 contadores en
dos grupos completamente independientes:
- 5 contadores de **preview** (proyectados): escritos *solo* por
  `RegistrarConteosPreview`, preservados tras la confirmación.
- 5 contadores de **confirmación** (reales): escritos *solo* por
  `RegistrarConteosConfirmacion`.

### Modelo final (10 contadores)

| Columna | Grupo | Escrito por |
|---|---|---|
| `filas_estimadas_a_crear` | preview | `RegistrarConteosPreview` |
| `filas_estimadas_a_actualizar` | preview | `RegistrarConteosPreview` |
| `filas_estimadas_a_omitir` | preview | `RegistrarConteosPreview` |
| `filas_estimadas_rechazadas` | preview | `RegistrarConteosPreview` |
| `filas_estimadas_con_advertencia` | preview | `RegistrarConteosPreview` |
| `filas_creadas` | confirmación | `RegistrarConteosConfirmacion` |
| `filas_actualizadas` | confirmación | `RegistrarConteosConfirmacion` |
| `filas_omitidas` | confirmación | `RegistrarConteosConfirmacion` |
| `filas_rechazadas` | confirmación | `RegistrarConteosConfirmacion` |
| `filas_con_advertencia` | confirmación | `RegistrarConteosConfirmacion` |

Columnas preservadas sin renombrar: `filas_omitidas`, `filas_rechazadas`,
`filas_con_advertencia` (mismos nombres, nuevo grupo semántico asignado
exclusivamente a confirmación).

### Regla de backfill por estado (migración M008)

La migración `M008_SepararContadoresPreviewConfirmacion` aplica el siguiente
backfill sobre datos existentes:

| Estado | Acción de backfill |
|---|---|
| `Confirmada` | `filas_creadas = filas_importadas` (mejor aproximación: el modelo anterior no distinguía creates de updates). Las 5 columnas `filas_estimadas_*` quedan en 0. |
| `PreviewGenerado` | Todos los nuevos campos en 0. Las columnas `filas_omitidas`, `filas_rechazadas`, `filas_con_advertencia` ya existían con los mismos nombres — no requieren acción. |
| `Fallida` | Igual que `PreviewGenerado`: todos los nuevos campos en 0. |

#### Nota de imprecisión histórica para estado `Confirmada`

El valor de `filas_importadas` representaba la suma de creates y updates
(`filas_creadas + filas_actualizadas` en el nuevo modelo). No es posible
reconstruir el split desde datos históricos. La asignación
`filas_creadas = filas_importadas` es la mejor aproximación disponible y es
aceptable porque el sistema está en fase piloto con datos reproducibles
(dataset Uyuni de prueba); la primera importación real del piloto de Caranavi
registrará conteos precisos desde el inicio.

### Justificación

- **Trazabilidad**: un usuario puede ver exactamente qué se proyectó en el
  preview y qué ocurrió realmente tras la confirmación, en la misma fila.
  Divergencias TOCTOU son visibles y auditables.
- **Inmutabilidad de preview**: tras la confirmación los estimados no cambian,
  lo que permite comparaciones post-facto (¿el proceso salió como se esperaba?).
- **Alineación con el modelo de dominio**: cada método del aggregate escribe en
  un conjunto disjunto de propiedades. No hay estado compartido entre fases.

---

## Consecuencias

- La tabla `dominio.importaciones` tiene 10 columnas de conteo vs. 4 anteriores.
- Los DTOs `ImportacionResumenDto` e `ImportacionDetalleDto` exponen los 10
  campos para permitir vistas por estado sin llamadas adicionales.
- Los handlers `ListarImportacionesHandler` y `ObtenerDetalleImportacionHandler`
  mapean los 10 campos.
- `ConfirmarImportacionHandler` y `GenerarPreviewImportacionHandler` no
  requieren cambios (ya llamaban los métodos correctos con parámetros correctos).
- Tests del aggregate: 13 tests (10 existentes actualizados + 3 nuevos:
  post-preview confirmación=0, post-confirmación estimados preservados,
  divergencia TOCTOU).

---

## Deuda resuelta

Las tres deudas registradas en la propuesta original quedan cerradas:

1. ~~Renombrar columnas persistidas~~ — resuelto por M008.
2. ~~Documentar en API la restricción de filtro por estado~~ — ya no aplica;
   los 10 campos son semánticamente unívocos por grupo.
3. Política unificada `DomainException` vs `Result<DomainError>` — sigue
   pendiente como deuda separada (ver sprint-03-cierre-formal.md).

---

## Archivos modificados (resolución Sprint 4)

| Archivo | Cambio |
|---|---|
| `SG.Domain/Importacion/Importacion.cs` | 10 propiedades, métodos con escritura disjunta |
| `SG.Infrastructure/.../ImportacionConfiguration.cs` | `HasColumnName` explícito para las 10 columnas |
| `SG.Contracts/Importacion/ImportacionResumenDto.cs` | 10 campos, sin `FilasImportadas` |
| `SG.Contracts/Importacion/ImportacionDetalleDto.cs` | 10 campos, sin `FilasImportadas` |
| `SG.Application/Importacion/Listar/ListarImportacionesHandler.cs` | mapeo a 10 campos |
| `SG.Application/Importacion/ObtenerDetalle/ObtenerDetalleImportacionHandler.cs` | mapeo a 10 campos |
| `SG.Infrastructure/.../Migrations/20260610231604_M008_...cs` | ADD×7 + backfill + DROP |
| `tests/SG.Domain.Tests/Importacion/ImportacionAggregateTests.cs` | 13 tests |

---

## Referencias

- Commit decisión parcial Sprint 3: 1c40545
- Commit resolución completa Sprint 4: 4ea3202
