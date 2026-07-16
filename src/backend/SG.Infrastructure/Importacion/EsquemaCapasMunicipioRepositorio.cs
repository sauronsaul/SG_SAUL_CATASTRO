using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.Importacion;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Importacion;

internal sealed class EsquemaCapasMunicipioRepositorio(ApplicationDbContext db)
    : IEsquemaCapasMunicipioRepositorio
{
    public async Task<IReadOnlyList<EsquemaCapaMunicipio>> ListarAsync(
        string municipioCodigo,
        CancellationToken ct = default) =>
        await db.EsquemasCapas
            .AsNoTracking()
            .Where(x => x.MunicipioCodigo == municipioCodigo)
            .OrderBy(x => x.TipoCapa)
            .ToListAsync(ct);
}
