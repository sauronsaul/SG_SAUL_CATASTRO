namespace SG.Contracts.Importacion;

public record ConfirmacionImportacionDto(
    Guid ImportacionId,
    int TotalFilas,
    int FilasCreadas,
    int FilasActualizadas,
    int FilasOmitidas,
    int FilasRechazadas,
    int FilasConAdvertencia);
