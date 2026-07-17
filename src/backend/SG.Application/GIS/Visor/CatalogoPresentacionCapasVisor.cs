using SG.Contracts.GIS;
using SG.Domain.Importacion;

namespace SG.Application.GIS.Visor;

public static class CatalogoPresentacionCapasVisor
{
    private static readonly Dictionary<TipoCapa, CapaVisorDto> PorTipo =
        new Dictionary<TipoCapa, CapaVisorDto>
        {
            [TipoCapa.AreasUrbanas] = Crear(TipoCapa.AreasUrbanas, "areas-urbanas", "Áreas urbanas", 10, 9, "#0D9488", true, true),
            [TipoCapa.Distritos] = Crear(TipoCapa.Distritos, "distritos", "Distritos", 20, 9, "#475569", true, true, campoEtiqueta: "nombre", minZoomEtiqueta: 11),
            [TipoCapa.ZonasValuacion] = Crear(TipoCapa.ZonasValuacion, "zonas", "Zonas de valuación", 30, 11, "#D97706", true, true, campoEtiqueta: "nombre_zona", minZoomEtiqueta: 13),
            [TipoCapa.Manzanas] = Crear(TipoCapa.Manzanas, "manzanas", "Manzanas", 40, 13, "#64748B", true, true),
            [TipoCapa.Predios] = Crear(TipoCapa.Predios, "parcelas", "Predios", 50, 15, "#0F766E", true, true),
            [TipoCapa.PrediosNoFotografiados] = Crear(TipoCapa.PrediosNoFotografiados, "predios-no-fotografiados", "Predios no fotografiados", 60, 16, "#7C3AED", true, true),
            [TipoCapa.Construcciones] = Crear(TipoCapa.Construcciones, "edificaciones", "Edificaciones", 70, 16, "#C2410C", true, true),
            [TipoCapa.Vias] = Crear(TipoCapa.Vias, "vias", "Vías", 80, 13, "#1D4ED8", false, true, campoEtiqueta: "nombre", minZoomEtiqueta: 15),
            [TipoCapa.PuntosGeodesicos] = Crear(TipoCapa.PuntosGeodesicos, "puntos-geodesicos", "Puntos geodésicos", 90, 11, "#DC2626", false, false, true, "puntos", 13),
        };

    private static readonly Dictionary<string, TipoCapa> PorNombre =
        PorTipo.ToDictionary(x => x.Value.Nombre, x => x.Key, StringComparer.Ordinal);

    public static CapaVisorDto Obtener(TipoCapa tipo) => PorTipo[tipo];

    public static bool IntentarResolver(string nombre, out TipoCapa tipo) =>
        PorNombre.TryGetValue(nombre, out tipo);

    private static CapaVisorDto Crear(
        TipoCapa tipo,
        string nombre,
        string titulo,
        int orden,
        int minZoom,
        string color,
        bool relleno,
        bool linea,
        bool circulo = false,
        string? campoEtiqueta = null,
        int? minZoomEtiqueta = null) =>
        new(tipo.ToString(), nombre, titulo, orden, minZoom, color, relleno, linea, circulo, campoEtiqueta, minZoomEtiqueta);
}
