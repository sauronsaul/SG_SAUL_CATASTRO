using SG.Domain.Importacion;

namespace SG.Application.Abstractions.Importacion;

public sealed record ResultadoInspeccionPaqueteVersionado(
    bool EsValido,
    IReadOnlySet<string> PerfilesPresentes,
    IReadOnlyList<string> Errores);

public interface IInspectorPaqueteVersionado
{
    ResultadoInspeccionPaqueteVersionado Inspeccionar(
        Stream paquete,
        IReadOnlyList<EsquemaCapaMunicipio> esquemaMunicipal);
}
