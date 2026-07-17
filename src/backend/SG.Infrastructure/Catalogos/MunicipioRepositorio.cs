using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.Catalogos;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Catalogos;

internal sealed class MunicipioRepositorio(ApplicationDbContext db) : IMunicipioRepositorio
{
    public Task<bool> ExistePorCodigoIneAsync(
        string codigoIne,
        CancellationToken ct = default) =>
        db.Municipios.AsNoTracking().AnyAsync(x => x.CodigoIne == codigoIne, ct);

    public Task<SG.Domain.Catalogos.Municipio?> ObtenerPorCodigoIneAsync(
        string codigoIne,
        CancellationToken ct = default) =>
        db.Municipios.AsNoTracking().FirstOrDefaultAsync(x => x.CodigoIne == codigoIne, ct);

    public async Task<IReadOnlyList<SG.Domain.Catalogos.Municipio>> ListarConDatasetActivoAsync(
        CancellationToken ct = default) =>
        await db.Municipios
            .AsNoTracking()
            .Where(m => db.DatasetVersiones.Any(v =>
                v.MunicipioCodigo == m.CodigoIne &&
                v.Estado == EstadoDatasetVersion.Activa))
            .OrderBy(m => m.Nombre)
            .ToListAsync(ct);
}
