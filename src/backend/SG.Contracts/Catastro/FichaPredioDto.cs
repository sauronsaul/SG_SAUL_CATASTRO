namespace SG.Contracts.Catastro;

public sealed record FichaPredioDto(
    Guid PredioId,
    Guid DatasetVersionId,
    int NumeroVersion,
    string MunicipioCodigo,
    int FilaOrigen,
    int Distrito,
    int Manzana,
    int Predio,
    string? CodigoCatastral,
    string? CodigoGeografico,
    string Estado,
    decimal SuperficieDeclaradaM2,
    decimal SuperficieGraficaM2,
    decimal? SuperficieOficialM2,
    string? PropietarioReferencia,
    string? TipoInmueble,
    string? NombreVia,
    string? Barrio,
    string? Direccion,
    string? UsoTerreno,
    string? TopografiaTerreno,
    string? ServicioAgua,
    string? ServicioLuz,
    string? ServicioAlcantarillado,
    string? ServicioTelefonia,
    GeometriaPlanarDto GeometriaPlanar,
    LimitesPredioDto Limites);

/// <summary>
/// Poligono expresado en coordenadas planas metricas EPSG:32719. Cada anillo
/// contiene posiciones [este, norte]; el primer anillo es exterior. El SRID
/// viaja explicitamente para impedir que se interprete como GeoJSON RFC 7946.
/// </summary>
public sealed record GeometriaPlanarDto(
    int Srid,
    string Tipo,
    double[][][] Coordenadas);

public sealed record LimitesPredioDto(
    double Oeste,
    double Sur,
    double Este,
    double Norte);
