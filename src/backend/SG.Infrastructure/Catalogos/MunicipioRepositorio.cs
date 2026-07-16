using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.Catalogos;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Catalogos;

internal sealed class MunicipioRepositorio(ApplicationDbContext db) : IMunicipioRepositorio
{
    public Task<bool> ExistePorCodigoIneAsync(
        string codigoIne,
        CancellationToken ct = default) =>
        db.Municipios.AsNoTracking().AnyAsync(x => x.CodigoIne == codigoIne, ct);
}
