using SG.Application.Common;
using SG.Domain.Catastro;

namespace SG.Application.Abstractions.Catastro;

public interface IPredioRepositorio
{
    Task<Predio?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExisteCodigoCatastralAsync(string codigoCatastral, CancellationToken ct = default);
    Task<PagedResult<Predio>> ListarAsync(int page, int pageSize, CancellationToken ct = default);
    void Agregar(Predio predio);
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
