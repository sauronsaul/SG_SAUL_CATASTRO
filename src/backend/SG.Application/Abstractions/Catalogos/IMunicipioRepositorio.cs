using SG.Domain.Catalogos;

namespace SG.Application.Abstractions.Catalogos;

public interface IMunicipioRepositorio
{
    Task<bool> ExistePorCodigoIneAsync(
        string codigoIne,
        CancellationToken ct = default);
    Task<Municipio?> ObtenerPorCodigoIneAsync(
        string codigoIne,
        CancellationToken ct = default);
    Task<IReadOnlyList<Municipio>> ListarConDatasetActivoAsync(
        CancellationToken ct = default);
}
