namespace SG.Contracts.Importacion;

public record PerfilImportacionResumenDto(
    Guid Id,
    string Nombre,
    string? Descripcion,
    string TipoCapa,
    int CantidadMapeos);
