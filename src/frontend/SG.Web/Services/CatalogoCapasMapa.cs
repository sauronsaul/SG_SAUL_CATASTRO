using SG.Web.Models;

namespace SG.Web.Services;

public static class CatalogoCapasMapa
{
    private static readonly IReadOnlyList<ConfiguracionCapaMapa> Capas =
    [
        new("distritos", "Distritos", 9, "#475569", true, true, "nombre", 10),
        new("zonas", "Zonas", 11, "#D97706", true, true, "nombre_zona", 12),
        new("manzanas", "Manzanas", 13, "#64748B", true, true),
        new("parcelas", "Parcelas", 15, "#0F766E", true, true),
        new("predios-no-fotografiados", "Predios no fotografiados", 16, "#7C3AED", true, true),
        new("edificaciones", "Edificaciones", 16, "#C2410C", true, true),
        new("vias", "Vías", 13, "#1D4ED8", false, true, "nombre", 15),
    ];

    public static IReadOnlyList<ConfiguracionCapaMapa> ObtenerTodas() => Capas;

    public static bool IntentarObtener(string nombre, out ConfiguracionCapaMapa? capa)
    {
        capa = Capas.FirstOrDefault(x => string.Equals(x.Nombre, nombre, StringComparison.Ordinal));
        return capa is not null;
    }

    public static IReadOnlyList<CapaEstiloMapa> ObtenerOrdenDibujo()
    {
        var resultado = new List<CapaEstiloMapa>();
        resultado.AddRange(Capas.Where(x => x.TieneRelleno)
            .Select(x => new CapaEstiloMapa(x.Nombre, TipoRepresentacionMapa.Relleno)));
        resultado.AddRange(Capas.Where(x => x.TieneLinea)
            .Select(x => new CapaEstiloMapa(x.Nombre, TipoRepresentacionMapa.Linea)));
        resultado.AddRange(Capas.Where(x => x.CampoEtiqueta is not null)
            .Select(x => new CapaEstiloMapa(x.Nombre, TipoRepresentacionMapa.Etiqueta)));
        return resultado;
    }
}
