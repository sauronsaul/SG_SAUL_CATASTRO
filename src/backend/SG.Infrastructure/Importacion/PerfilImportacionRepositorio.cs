using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.Importacion;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Importacion;

internal sealed class PerfilImportacionRepositorio(ApplicationDbContext db)
    : IPerfilImportacionRepositorio
{
    public async Task<PerfilImportacion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.PerfilesImportacion
            .Include(p => p.Mapeos)
                .ThenInclude(m => m.Equivalencias)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<IReadOnlyList<PerfilImportacion>> ListarAsync(CancellationToken ct = default) =>
        await db.PerfilesImportacion
            .AsNoTracking()
            .Include(p => p.Mapeos)
            .OrderBy(p => p.Nombre)
            .ToListAsync(ct);
}
