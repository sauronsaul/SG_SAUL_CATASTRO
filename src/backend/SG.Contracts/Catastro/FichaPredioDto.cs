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
    LimitesPredioDto Limites);

public sealed record LimitesPredioDto(
    double Oeste,
    double Sur,
    double Este,
    double Norte);
