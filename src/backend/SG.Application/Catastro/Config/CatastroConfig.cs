namespace SG.Application.Catastro.Config;

public sealed class CatastroConfig
{
    public string MunicipioCodigo { get; init; } = string.Empty;
    public CodigoCatastralConfig CodigoCatastral { get; init; } = new();
}

public sealed class CodigoCatastralConfig
{
    public string DepartamentoCodigo { get; init; } = string.Empty;
    public string ProvinciaCodigo { get; init; } = string.Empty;
    public string MunicipioCodigo { get; init; } = string.Empty;
}
