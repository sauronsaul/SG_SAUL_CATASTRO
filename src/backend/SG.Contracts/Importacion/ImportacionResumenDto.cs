namespace SG.Contracts.Importacion;

public record ImportacionResumenDto(
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
    int FilasOmitidas);
