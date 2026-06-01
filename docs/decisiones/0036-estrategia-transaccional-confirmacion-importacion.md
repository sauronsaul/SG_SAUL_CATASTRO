# ADR 0036 — Estrategia transaccional en ConfirmarImportacionHandler

**Estado**: Aceptado
**Fecha**: 2026-05-23
**Autor**: Saul Gutierrez
**Sprint**: 3

---

## Contexto

La confirmación de una importación (Fase 2) ejecuta múltiples escrituras heterogéneas en una sola solicitud HTTP:

- `N` predios nuevos (INSERT) o actualizados (UPDATE) vía `PredioRepositorio`
- `M` construcciones agregadas a predios padre (INSERT en tabla `construcciones`)
- El registro `Importacion` transiciona de `PreviewGenerado` → `Confirmada` y se actualizan sus conteos vía `ImportacionRepositorio`

Si cualquiera de estas escrituras falla a mitad del proceso, el catastro quedaría en estado inconsistente: predios parcialmente importados con la `Importacion` aún en `PreviewGenerado`, o viceversa.

## Decisión

El handler guarda **todos los cambios en un único `SaveChangesAsync`** al final del método `Handle`, después de que toda la lógica de dominio se ha aplicado en memoria:

```csharp
importacion.RegistrarResultados(...);
importacion.Confirmar();
await predios.GuardarCambiosAsync(cancellationToken);  // único punto de commit
```

Esto es posible porque tanto `PredioRepositorio` como `ImportacionRepositorio` inyectan el **mismo `ApplicationDbContext` con ciclo de vida `Scoped`** (un contexto por request HTTP). EF Core rastrea todos los cambios sobre ese contexto — predios nuevos, predios modificados, construcciones agregadas, estado de importación — y los materializa en una única transacción de base de datos.

No se usa `IDbContextTransaction` explícita: EF Core envuelve automáticamente un `SaveChangesAsync` en una transacción de PostgreSQL cuando hay múltiples operaciones pendientes.

## Consecuencias

**Positivas:**
- **Todo o nada**: si falla cualquier INSERT/UPDATE, PostgreSQL hace rollback completo. El catastro nunca queda a medias.
- **Sin código de infraestructura adicional**: no requiere `BeginTransactionAsync`, `CommitAsync`, `RollbackAsync` explícitos. El patrón Unit of Work de EF Core lo resuelve.
- **Simple de razonar**: un solo punto de fallo, fácil de testear.

**Negativas / restricciones:**
- El handler carga en memoria todos los predios a actualizar antes de hacer commit. Para importaciones masivas (>20 000 filas), el consumo de memoria puede ser significativo. Se acepta para el municipio piloto (~12 000 predios).
- No hay `SaveChangesAsync` parcial: si la lógica de dominio rechaza una fila, el recuento refleja el rechazo pero no interrumpe el lote.

## Alternativas descartadas

| Alternativa | Motivo de descarte |
|---|---|
| Múltiples `SaveChangesAsync` (uno por predio) | Inconsistencia si falla a mitad; N round-trips innecesarios |
| Transacción explícita `IDbContextTransaction` | Verbosidad sin beneficio cuando el Unit of Work ya lo cubre |
| Outbox / saga | Sobreingeniería para un proceso sincrónico de municipio pequeño |

## Límites conocidos — aceptados para el piloto

### Volumen de filas a escala Uyuni (~12.000 predios + ~18.000 construcciones)

Un `SaveChangesAsync` de confirmación completa persiste aproximadamente:

| Tipo de entidad | Cantidad estimada |
|---|---|
| Predios (INSERT o UPDATE) | ~12.000 |
| HistorialEstado (uno por predio) | ~12.000 |
| Construcciones (INSERT) | ~18.000 |
| AuditoriaEntidad (una por entidad anterior) | ~42.001 |
| **Total de filas en una transacción** | **~84.002** |

EF Core/Npgsql agrupa estas operaciones en batches de 1.000 statements por `DbCommand`. Son ~84 commands en secuencia, todos dentro de la misma transacción PostgreSQL. Cada command individual está muy por debajo del `CommandTimeout = 300s` configurado.

### CommandTimeout

El `CommandTimeout` global se elevó de 30s a **300s** en `DependencyInjection.AddPersistencia`. Aplica a todos los comandos del contexto. El riesgo de 30s era teórico (cada batch de 1.000 INSERT raramente supera ese límite), pero la elevación evita falsos positivos en hardware lento del municipio.

### Timeout HTTP (deuda técnica — Sprint 4/5)

El riesgo real no es el SQL timeout sino el **timeout HTTP de Caddy/nginx**: si los ~84.000 inserts + serialización JSON del `AuditoriaInterceptor` toman más de lo que el proxy permite (default 30s en muchos reverse proxies), el cliente recibe un error aunque PostgreSQL complete la transacción correctamente.

**Configuración requerida en Caddy** para el endpoint de confirmación:
```caddy
@confirmar {
    method POST
    path /api/importaciones/*/confirmar
}
reverse_proxy @confirmar backend:8080 {
    transport http {
        response_header_timeout 360s
        dial_timeout             10s
    }
}
```

**Deuda técnica registrada para Sprint 4/5**: cuando el sistema escale a municipios con >20.000 predios (p. ej. Cochabamba, Santa Cruz), la importación deberá volverse **asíncrona**: el endpoint `POST /confirmar` devuelve un `202 Accepted` con un `jobId`, y el cliente consulta `GET /importaciones/{id}/estado` hasta obtener `Confirmada`. Esto requiere rediseñar el handler y deprecar el modelo síncrono actual.

### Geometría en auditoría

El `AuditoriaInterceptor` excluye la propiedad `Poligono` (tipo NTS `Polygon`) y omite el entry de `GeometriaPredial` por completo. Sin esta corrección, cada predio importado con geometría generaría un blob JSON con las coordenadas del polígono en la columna `valor_nuevo` de la tabla `auditoria`, multiplicando el tamaño de la tabla por un factor de ×10 o más.

### Concurrencia entre preview y confirmación — ventana TOCTOU

El handler implementa un patrón check-recompute para protegerse de
cambios de estado entre la generación del preview y la confirmación:
`ConfirmarCapaPrediosAsync` carga los predios existentes con tracking
y, en línea ~135, llama a `ClasificadorAccionPreview.Clasificar` con
el diccionario de estados recién cargado de BD — sin confiar en la
acción que calculó el preview en su momento.

Verificado en la prueba P6b del Punto de Control 3.2 (Sprint 3):
un predio en estado `Importado` al momento del preview (clasificado
como `Actualizar`), validado manualmente entre el preview y la
confirmación, queda correctamente como `Omitir` en la respuesta de
confirmación (`filasOmitidas = 2` cuando se esperaba `1`). La
validación humana sobrevive a la re-importación.

**Límite del mecanismo — concurrencia simultánea.** El check-recompute
de línea 135 clasifica sobre el estado capturado en
`ObtenerParaActualizarPorTripletasAsync` (~línea 119), no sobre el
estado vivo al momento del commit. Si un técnico ejecuta
`UPDATE predios SET estado='Validado' WHERE id=...` *después* de esa
carga inicial pero *antes* del `SaveChangesAsync` final, el snapshot
trackeado por EF Core sigue mostrando `Importado` y el confirmador
pisaría la validación con un `Actualizar`. La ventana observada
empíricamente es de ~10-14s para 12.000 predios — suficientemente
grande para ser explotable en un entorno multi-usuario real.

El nivel de aislamiento por defecto de PostgreSQL en .NET/Npgsql es
`READ COMMITTED`, que NO previene este caso: lecturas posteriores
dentro de la misma transacción ven los datos del snapshot inicial,
pero la transacción que valida el predio (ejecutada fuera del handler
de confirmación) puede hacer commit independientemente.

**Decisión Sprint 3:** aceptar el límite. P6b prueba el patrón
secuencial; la concurrencia simultánea queda como riesgo conocido
del piloto Uyuni (un único técnico operando, baja probabilidad de
solapamiento real). No se implementa mitigación en Sprint 3.

**Opciones a evaluar en Sprint 4** cuando se diseñe la importación
asíncrona (ver "Timeout HTTP" arriba):

| Opción | Mecanismo | Costo |
|---|---|---|
| Aislamiento `Serializable` | `IDbContextTransaction` con `IsolationLevel.Serializable` envolviendo todo el handler | Bajo en código, alto en throughput (retries por conflictos) |
| Control de concurrencia optimista | Columna `xmin` o `version` en `predios`; reintento del handler ante conflicto | Medio en código, predecible en throughput |
| Re-lectura intra-transacción | Antes del `SaveChangesAsync`, re-cargar estados de los predios afectados y volver a clasificar; si difieren del snapshot, omitir esas filas | Bajo en código, sesgado a omisión en caso de duda |

La decisión específica se documentará en un ADR separado del Sprint 4
cuando se rediseñe el handler para soportar importación asíncrona,
ya que ambos cambios tocan el mismo código.

## Referencias

- ADR 0031 — Estrategia de repositorios con EF Core (Unit of Work implícito)
- ADR 0035 — Deuda técnica: limpieza de previews huérfanos en MinIO
