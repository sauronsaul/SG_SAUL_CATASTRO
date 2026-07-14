namespace SG.Web.Models;

public sealed record ConfiguracionCapaMapa(
    string Nombre,
    string Titulo,
    int MinZoom,
    string Color,
    bool TieneRelleno,
    bool TieneLinea,
    string? CampoEtiqueta = null,
    int? MinZoomEtiqueta = null);

public enum TipoRepresentacionMapa
{
    Relleno = 0,
    Linea = 1,
    Etiqueta = 2,
}

public sealed record CapaEstiloMapa(string NombreCapa, TipoRepresentacionMapa Tipo);
