using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.Catastro;
using SG.Domain.Catalogos;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Catastro;

internal sealed class UsoSueloRepositorio(ApplicationDbContext db) : IUsoSueloRepositorio
{
    public async Task<UsoSuelo?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.UsosSuelo.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public async Task<bool> ExisteAsync(Guid id, CancellationToken ct = default) =>
        await db.UsosSuelo.AnyAsync(u => u.Id == id, ct);

    public async Task<IReadOnlyList<UsoSuelo>> ListarActivosAsync(CancellationToken ct = default) =>
        await db.UsosSuelo
            .AsNoTracking()
            .Where(u => u.Activo)
            .OrderBy(u => u.Orden)
            .ToListAsync(ct);
}
