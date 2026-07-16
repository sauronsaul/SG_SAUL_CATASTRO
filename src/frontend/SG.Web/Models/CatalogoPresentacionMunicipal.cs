namespace SG.Web.Models;

public static class CatalogoPresentacionMunicipal
{
    public const string AyudaPendiente =
        "Código del catálogo municipal pendiente de diccionario oficial.";

    private static readonly IReadOnlyDictionary<string, string?> Etiquetas =
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["CMC"] = null,
            ["COM"] = null,
            ["CUL"] = null,
            ["DEP"] = null,
            ["EDU"] = null,
            ["IND"] = null,
            ["OFI"] = null,
            ["REC"] = null,
            ["REL"] = null,
            ["SAL"] = null,
            ["SER"] = null,
            ["SIN"] = null,
            ["TRR"] = null,
            ["TRU"] = null,
            ["VIV"] = null,
        };

    public static IReadOnlyCollection<string> CodigosConocidos => Etiquetas.Keys.ToArray();

    public static PresentacionCodigoMunicipal Crear(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return new PresentacionCodigoMunicipal("No registrado", null, false);

        var codigoCrudo = valor.Trim();
        var codigoBusqueda = codigoCrudo.ToUpperInvariant();
        if (!Etiquetas.TryGetValue(codigoBusqueda, out var etiqueta))
            return new PresentacionCodigoMunicipal(codigoCrudo, AyudaPendiente, false);

        var texto = string.IsNullOrWhiteSpace(etiqueta)
            ? $"{codigoCrudo} — código de origen"
            : $"{codigoCrudo} — {etiqueta}";
        return new PresentacionCodigoMunicipal(texto, AyudaPendiente, true);
    }
}

public sealed record PresentacionCodigoMunicipal(
    string Texto,
    string? Ayuda,
    bool EsCodigoConocido);
