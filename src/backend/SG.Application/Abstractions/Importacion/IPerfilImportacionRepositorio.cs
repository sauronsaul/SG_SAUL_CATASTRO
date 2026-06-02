using SG.Domain.Importacion;

namespace SG.Application.Abstractions.Importacion;

public interface IPerfilImportacionRepositorio
{
    Task<PerfilImportacion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PerfilImportacion>> ListarAsync(CancellationToken ct = default);
}
