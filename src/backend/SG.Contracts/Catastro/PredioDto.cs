namespace SG.Contracts.Catastro;

public sealed record PredioDto(
    Guid Id,
    string? CodigoCatastral,
    string Estado,
    decimal SuperficieDeclarada,
    decimal? SuperficieSig,
    decimal? SuperficieOficial,
    Guid UsoSueloId,
    string UbicacionZona,
    string UbicacionManzana,
    string UbicacionLote,
    string? UbicacionBarrio,
    string? UbicacionDireccion,
    string? UbicacionReferencia,
    bool TieneGeometria,
    double? AreaM2,
    DateTime CreadoAt,
    IReadOnlyList<HistorialEstadoDto> Historial);

public sealed record PredioResumenDto(
    Guid Id,
    string? CodigoCatastral,
    string Estado,
    decimal SuperficieDeclarada,
    Guid UsoSueloId,
    string UbicacionZona,
    string UbicacionManzana,
    string UbicacionLote,
    string? UbicacionBarrio,
    string? UbicacionDireccion,
    DateTime CreadoAt);

public sealed record HistorialEstadoDto(
    Guid Id,
    string EstadoAnterior,
    string EstadoNuevo,
    Guid CambiadoPor,
    DateTime CambiadoAt,
    string? Observaciones);
