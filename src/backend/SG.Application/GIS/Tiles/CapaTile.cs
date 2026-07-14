namespace SG.Application.GIS.Tiles;

public enum CapaTile
{
    Parcelas,
    Edificaciones,
    PrediosNoFotografiados,
    Manzanas,
    Distritos,
    Zonas,
    Vias,
}

public static class CatalogoCapasTile
{
    private static readonly Dictionary<string, CapaTile> PorNombre =
        new Dictionary<string, CapaTile>(StringComparer.Ordinal)
        {
            ["parcelas"] = CapaTile.Parcelas,
            ["edificaciones"] = CapaTile.Edificaciones,
            ["predios-no-fotografiados"] = CapaTile.PrediosNoFotografiados,
            ["manzanas"] = CapaTile.Manzanas,
            ["distritos"] = CapaTile.Distritos,
            ["zonas"] = CapaTile.Zonas,
            ["vias"] = CapaTile.Vias,
        };

    public static bool IntentarResolver(string nombre, out CapaTile capa) =>
        PorNombre.TryGetValue(nombre, out capa);

    public static string ObtenerNombre(CapaTile capa) => capa switch
    {
        CapaTile.Parcelas => "parcelas",
        CapaTile.Edificaciones => "edificaciones",
        CapaTile.PrediosNoFotografiados => "predios-no-fotografiados",
        CapaTile.Manzanas => "manzanas",
        CapaTile.Distritos => "distritos",
        CapaTile.Zonas => "zonas",
        CapaTile.Vias => "vias",
        _ => throw new ArgumentOutOfRangeException(nameof(capa), capa, null),
    };
}
