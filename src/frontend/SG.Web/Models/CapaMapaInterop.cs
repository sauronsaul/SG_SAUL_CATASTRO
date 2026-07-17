using System.Text.Json.Serialization;
using SG.Contracts.GIS;

namespace SG.Web.Models;

public sealed record CapaMapaInterop(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("minZoom")] int MinZoom,
    [property: JsonPropertyName("color")] string Color,
    [property: JsonPropertyName("tieneRelleno")] bool TieneRelleno,
    [property: JsonPropertyName("tieneLinea")] bool TieneLinea,
    [property: JsonPropertyName("tieneCirculo")] bool TieneCirculo,
    [property: JsonPropertyName("campoEtiqueta")] string? CampoEtiqueta,
    [property: JsonPropertyName("minZoomEtiqueta")] int? MinZoomEtiqueta,
    [property: JsonPropertyName("visible")] bool Visible)
{
    public static CapaMapaInterop Crear(CapaVisorDto capa, bool visible) => new(
        capa.Nombre,
        capa.MinZoom,
        capa.Color,
        capa.TieneRelleno,
        capa.TieneLinea,
        capa.TieneCirculo,
        capa.CampoEtiqueta,
        capa.MinZoomEtiqueta,
        visible);
}
