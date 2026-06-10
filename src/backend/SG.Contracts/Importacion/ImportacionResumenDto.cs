namespace SG.Contracts.Importacion;

public record ImportacionResumenDto(
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
    int FilasConAdvertencia);
