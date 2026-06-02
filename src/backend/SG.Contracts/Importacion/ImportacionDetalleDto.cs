namespace SG.Contracts.Importacion;

public record ImportacionDetalleDto(
    Guid ImportacionId,
    string NombreArchivo,
    string Estado,
    DateTime FechaImportacion,
    Guid ImportadoPorId,
    Guid PerfilId,
    int TotalFilas,
    int FilasImportadas,
    int FilasConAdvertencia,
    int FilasRechazadas,
    int FilasOmitidas,
    // Sólo se puebla cuando Estado == PreviewGenerado.
    // Para otros estados devuelve null — las filas ya fueron procesadas.
    IReadOnlyList<FilaPreviewDto>? Filas);
