using System.Text.Json.Serialization;

namespace SG.Web.Models;

public sealed record CapaMapaInterop(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("minZoom")] int MinZoom,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("tieneRelleno")] bool TieneRelleno,
    [property: JsonPropertyName("tieneLinea")] bool TieneLinea,
    [property: JsonPropertyName("campoEtiqueta")] string? CampoEtiqueta,
    [property: JsonPropertyName("minZoomEtiqueta")] int? MinZoomEtiqueta,
    [property: JsonPropertyName("visible")] bool Visible)
{
    public static CapaMapaInterop Crear(ConfiguracionCapaMapa capa, bool visible) => new(
        capa.Nombre,
        capa.MinZoom,
        capa.Color,
        capa.TieneRelleno,
        capa.TieneLinea,
        capa.CampoEtiqueta,
        capa.MinZoomEtiqueta,
        visible);
}
