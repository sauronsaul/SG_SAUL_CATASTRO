namespace SG.Domain.Importacion;

public sealed record DefinicionCapaVersionadaUyuni(
    string NombrePerfil,
    TipoCapa TipoCapa,
    string NombreArchivoShp,
    string NombreTabla);

public static class DefinicionesCapasVersionadasUyuni
{
    public const string MunicipioCodigo = "UYUNI";

    public static readonly IReadOnlyList<DefinicionCapaVersionadaUyuni> Todas =
    [
        new("uyuni-versionado-parcelas", TipoCapa.Predios, "PRE_SIS_UYU.shp", "capa_parcelas"),
        new("uyuni-versionado-edificaciones", TipoCapa.Construcciones, "EDI_SIS_UYU.shp", "capa_edificaciones"),
        new("uyuni-versionado-predios-no-fotografiados", TipoCapa.PrediosNoFotografiados, "PRE_NO_FOT.shp", "capa_predios_no_fotografiados"),
        new("uyuni-versionado-manzanas", TipoCapa.Manzanas, "MAN_SIS_UYU.shp", "capa_manzanas"),
        new("uyuni-versionado-distritos", TipoCapa.Distritos, "DIS_SIS_UYU.shp", "capa_distritos"),
        new("uyuni-versionado-zonas", TipoCapa.ZonasValuacion, "ZONA_SIS_UYU.shp", "capa_zonas"),
        new("uyuni-versionado-vias", TipoCapa.Vias, "VIA_INFO_UYU.shp", "capa_vias"),
    ];
}
