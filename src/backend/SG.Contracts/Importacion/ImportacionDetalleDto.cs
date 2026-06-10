namespace SG.Contracts.Importacion;

public record ImportacionDetalleDto(
    Guid ImportacionId,
    string NombreArchivo,
    string Estado,
    DateTime FechaImportacion,
    Guid ImportadoPorId,
    Guid PerfilId,
    int TotalFilas,
    // Conteos de preview (proyectados)
    int FilasEstimadasACrear,
    int FilasEstimadasAActualizar,
    int FilasEstimadasAOmitir,
    int FilasEstimadasRechazadas,
    int FilasEstimadasConAdvertencia,
    // Conteos de confirmación (reales)
    int FilasCreadas,
    int FilasActualizadas,
    int FilasOmitidas,
    int FilasRechazadas,
    int FilasConAdvertencia,
    // Sólo se puebla cuando Estado == PreviewGenerado.
    // Para otros estados devuelve null — las filas ya fueron procesadas.
    IReadOnlyList<FilaPreviewDto>? Filas);
