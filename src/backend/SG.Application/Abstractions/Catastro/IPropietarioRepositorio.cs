using SG.Application.Common;
using SG.Domain.Catastro;

namespace SG.Application.Abstractions.Catastro;

public interface IPropietarioRepositorio
{
    Task<Propietario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExisteCedulaAsync(string cedula, CancellationToken ct = default);
    Task<bool> ExisteNitAsync(string nit, CancellationToken ct = default);
    Task<PagedResult<Propietario>> ListarAsync(int page, int pageSize, string? busqueda, CancellationToken ct = default);
    void Agregar(Propietario propietario);
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
