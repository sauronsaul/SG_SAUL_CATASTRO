using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.GIS;

internal sealed class CoordenadasService(ApplicationDbContext db) : ICoordenadasService
{
    public async Task<string> RepoyectarA32719Async(
        string wkt,
        int sridOrigen,
        CancellationToken ct = default)
    {
        var sql = $"SELECT ST_AsText(ST_Transform(ST_GeomFromText({{0}}, {{1}}), 32719))";

        var resultado = await db.Database
            .SqlQueryRaw<string>(sql, wkt, sridOrigen)
            .FirstAsync(ct);

        return resultado;
    }
}
