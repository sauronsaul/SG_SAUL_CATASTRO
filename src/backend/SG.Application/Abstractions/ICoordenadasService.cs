namespace SG.Application.Abstractions;

public interface ICoordenadasService
{
    /// <summary>
    /// Reproyecta un polígono en WKT desde sridOrigen a EPSG:32719 usando PostGIS ST_Transform.
    /// </summary>
    Task<string> RepoyectarA32719Async(string wkt, int sridOrigen, CancellationToken ct = default);
}
