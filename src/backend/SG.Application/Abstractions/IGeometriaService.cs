using SG.Domain.Catastro.ValueObjects;
using SG.Domain.Common;

namespace SG.Application.Abstractions;

public interface IGeometriaService
{
    /// <summary>
    /// Parsea WKT o GeoJSON, repoyecta a SRID 32719 si es necesario y crea GeometriaPredial.
    /// Si srid es null: asume 32719 para WKT o 4326 para GeoJSON.
    /// </summary>
    Task<Result<GeometriaPredial>> ParsearAsync(
        string geometria,
        string formato,
        int? srid,
        CancellationToken ct = default);
}
