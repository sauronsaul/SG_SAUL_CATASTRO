namespace SG.Contracts.Importacion;

public enum AccionPreviewFila
{
    Crear = 1,
    Actualizar = 2,
    Omitir = 3,
    Rechazada = 4,
}

public record FilaPreviewDto(
    int NumeroFila,
    AccionPreviewFila Accion,
    IReadOnlyDictionary<string, string?> Valores,
    IReadOnlyList<string> Advertencias,
    IReadOnlyList<string> Errores);

public record PreviewImportacionDto(
    Guid ImportacionId,
    string NombreArchivo,
    int TotalFilas,
    int FilasACrear,
    int FilasAActualizar,
    int FilasAOmitir,
    int FilasRechazadas,
    int FilasConAdvertencia,
    // Máximo 20 filas por categoría (Crear/Actualizar/Omitir/ConAdvertencia/Rechazadas).
    // El detalle completo se obtiene vía GET /api/importaciones/{id}.
    IReadOnlyList<FilaPreviewDto> MuestraFilas);
