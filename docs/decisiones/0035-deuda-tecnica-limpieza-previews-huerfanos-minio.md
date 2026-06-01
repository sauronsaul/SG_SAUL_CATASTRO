# ADR 0035 — Deuda técnica: limpieza de previews huérfanos en MinIO

**Estado**: Pendiente  
**Fecha**: 2026-05-23  
**Sprint**: 3 (identificada en T7)  
**Para resolver en**: Sprint 4

---

## Contexto

El handler `GenerarPreviewImportacionHandler` (T7) sube el .zip de importación
a MinIO **antes** de que el técnico confirme la operación, y registra una entidad
`Importacion` en estado `PreviewGenerado`.

Si el técnico:
- Abandona el flujo (cierra sesión, cierra el navegador)
- Decide no confirmar el preview
- La API cae entre el preview y el confirm

...el registro `Importacion` queda en `PreviewGenerado` indefinidamente y el .zip
en MinIO queda huérfano, sin ser nunca eliminado.

## Impacto potencial

| Riesgo | Nivel |
|---|---|
| Acumulación de .zip en MinIO | Bajo en MVP (volumen bajo), creciente en producción |
| Registros basura en `dominio.importaciones` | Bajo — no afectan lógica, solo ruido en reportes |
| Costo de almacenamiento MinIO | Irrelevante en modo local, potencial en modo servidor |

## Decisión tomada en Sprint 3

Aceptar la deuda. El comportamiento actual es correcto para el flujo feliz
(preview → confirm en la misma sesión). La limpieza no bloquea el MVP.

## Tarea pendiente para Sprint 4

Implementar un **job de limpieza** que:

1. Consulta `dominio.importaciones` con `estado = 'PreviewGenerado'`
   y `fecha_importacion < ahora - 48h` (configurable).
2. Para cada registro encontrado:
   - Llama a `IMinioService.MoverAPapeleraAsync(ruta_minio_zip)`.
   - Actualiza el estado de la entidad a `Fallida` con motivo
     `"Preview no confirmado — limpiado automáticamente"`.
3. Se ejecuta como `IHostedService` (background task de .NET) o como
   endpoint `POST /api/admin/importaciones/limpiar-huerfanos`
   disparado manualmente por un Admin en MVP.
4. La ventana de 48 h debe ser configurable en `appsettings.json`
   bajo `Importacion:HorasRetencionPreview`.

## Diseño sugerido (para no olvidar)

```csharp
// En Importacion.cs — método de dominio a agregar en Sprint 4:
public void MarcarHuerfana(string motivo)
{
    Estado = EstadoImportacion.Fallida;
    // opcional: guardar motivo en un campo DetalleError (migración futura)
}
```

```csharp
// Interfaz repositorio a extender:
Task<IReadOnlyList<Importacion>> ObtenerPreviewsAnteriorAAsync(
    DateTime umbral, CancellationToken ct = default);
```

## Referencias

- T7 — `GenerarPreviewImportacionHandler` en `SG.Application/Importacion/GenerarPreview/`
- T8 — Confirmar importación (Sprint 3, pendiente): al implementar confirm,
  los previews confirmados cambian de estado y sus .zip pasan a ser permanentes.
- `IMinioService.MoverAPapeleraAsync` — ya implementado, nunca elimina físicamente.
