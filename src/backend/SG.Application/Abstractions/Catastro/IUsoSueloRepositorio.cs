using SG.Domain.Catalogos;

namespace SG.Application.Abstractions.Catastro;

public interface IUsoSueloRepositorio
{
    Task<UsoSuelo?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExisteAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<UsoSuelo>> ListarActivosAsync(CancellationToken ct = default);
}
