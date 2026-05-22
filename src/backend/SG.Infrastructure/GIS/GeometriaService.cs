using System.Text;
using System.Text.Json;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SG.Application.Abstractions;
using SG.Domain.Catastro.ValueObjects;
using SG.Domain.Common;

namespace SG.Infrastructure.GIS;

internal sealed class GeometriaService(ICoordenadasService coordenadas) : IGeometriaService
{
    private const int SridObjetivo = GeometriaPredial.SridObligatorio;

    public async Task<Result<GeometriaPredial>> ParsearAsync(
        string geometria,
        string formato,
        int? srid,
        CancellationToken ct = default)
    {
        try
        {
            string wkt;
            int sridOrigen;

            if (formato.Equals("WKT", StringComparison.OrdinalIgnoreCase))
            {
                wkt = geometria.Trim();
                sridOrigen = srid ?? SridObjetivo;
            }
            else if (formato.Equals("GeoJSON", StringComparison.OrdinalIgnoreCase))
            {
                var parseResult = GeoJsonAWkt(geometria);
                if (parseResult is null)
                    return Result.Failure<GeometriaPredial>(GeometriaPredialErrores.GeometriaInvalida);
                wkt = parseResult;
                sridOrigen = srid ?? 4326;
            }
            else
            {
                return Result.Failure<GeometriaPredial>(
                    new DomainError("Geometria.FormatoInvalido", $"Formato no soportado: {formato}. Use 'WKT' o 'GeoJSON'."));
            }

            if (sridOrigen != SridObjetivo)
                wkt = await coordenadas.RepoyectarA32719Async(wkt, sridOrigen, ct);

            var reader = new WKTReader(NtsGeometryServices.Instance);
            var geom = reader.Read(wkt);
            geom.SRID = SridObjetivo;

            if (geom is not Polygon poligono)
                return Result.Failure<GeometriaPredial>(
                    new DomainError("Geometria.TipoInvalido", "La geometría debe ser un Polygon."));

            return GeometriaPredial.Crear(poligono);
        }
        catch (Exception ex) when (ex is ParseException or JsonException or InvalidOperationException)
        {
            return Result.Failure<GeometriaPredial>(GeometriaPredialErrores.GeometriaInvalida);
        }
    }

    private static string? GeoJsonAWkt(string geoJson)
    {
        using var doc = JsonDocument.Parse(geoJson);
        var root = doc.RootElement;

        JsonElement geometry;

        if (root.TryGetProperty("type", out var typeProp) &&
            typeProp.GetString() == "Feature" &&
            root.TryGetProperty("geometry", out var geomProp))
        {
            geometry = geomProp;
        }
        else
        {
            geometry = root;
        }

        if (!geometry.TryGetProperty("type", out var geomType) ||
            geomType.GetString() != "Polygon")
            return null;

        if (!geometry.TryGetProperty("coordinates", out var coordsRoot))
            return null;

        var rings = coordsRoot.EnumerateArray().ToList();
        if (rings.Count == 0)
            return null;

        var sb = new StringBuilder("POLYGON (");
        for (int r = 0; r < rings.Count; r++)
        {
            if (r > 0) sb.Append(", ");
            sb.Append('(');
            var points = rings[r].EnumerateArray().ToList();
            for (int i = 0; i < points.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                var arr = points[i].EnumerateArray().ToList();
                sb.Append(arr[0].GetDouble());
                sb.Append(' ');
                sb.Append(arr[1].GetDouble());
            }
            sb.Append(')');
        }
        sb.Append(')');

        return sb.ToString();
    }
}
