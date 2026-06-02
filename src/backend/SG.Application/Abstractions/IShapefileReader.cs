using NetTopologySuite.Geometries;

namespace SG.Application.Abstractions;

public record RegistroCrudoShapefile(
    Geometry? Geometria,
    IReadOnlyDictionary<string, object?> Atributos,
    bool ProyeccionDesconocida,
    string? SridOrigenWkt,
    string? ErrorGeometria = null);

public interface IShapefileReader
{
    IEnumerable<RegistroCrudoShapefile> Leer(string rutaShp);
}
