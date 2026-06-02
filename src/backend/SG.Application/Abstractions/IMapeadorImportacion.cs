using NetTopologySuite.Geometries;
using SG.Domain.Importacion;

namespace SG.Application.Abstractions;

public enum ClasificacionFila
{
    Ok = 1,
    Advertencia = 2,
    Rechazada = 3
}

public record ResultadoMapeoFila(
    int NumeroFila,
    ClasificacionFila Clasificacion,
    IReadOnlyDictionary<string, string?> ValoresMapeados,
    Geometry? Geometria,
    IReadOnlyList<string> Advertencias,
    IReadOnlyList<string> Errores);

public interface IMapeadorImportacion
{
    ResultadoMapeoFila Mapear(
        RegistroCrudoShapefile registro,
        PerfilImportacion perfil,
        int numeroFila);
}
