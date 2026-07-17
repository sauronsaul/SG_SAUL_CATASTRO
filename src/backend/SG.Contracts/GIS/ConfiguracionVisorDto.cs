namespace SG.Contracts.GIS;

public sealed record MunicipioVisorDto(
    string Codigo,
    string Nombre,
    string NombreOficial);

public sealed record LimitesVisorDto(
    double Oeste,
    double Sur,
    double Este,
    double Norte);

public sealed record CapacidadesVisorDto(bool TienePredios);

public sealed record CapaVisorDto(
    string Tipo,
    string Nombre,
    string Titulo,
    int Orden,
    int MinZoom,
    string Color,
    bool TieneRelleno,
    bool TieneLinea,
    bool TieneCirculo,
    string? CampoEtiqueta,
    int? MinZoomEtiqueta);

public sealed record ConfiguracionVisorDto(
    MunicipioVisorDto Municipio,
    int NumeroVersionActiva,
    LimitesVisorDto Bbox,
    IReadOnlyList<CapaVisorDto> Capas,
    CapacidadesVisorDto Capacidades);
