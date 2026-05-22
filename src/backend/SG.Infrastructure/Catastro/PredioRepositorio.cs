using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.Catastro;
using SG.Application.Common;
using SG.Domain.Catastro;
using SG.Domain.Catastro.ValueObjects;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Catastro;

internal sealed class PredioRepositorio(ApplicationDbContext db) : IPredioRepositorio
{
    public async Task<Predio?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Predios
            .Include(p => p.Historial)
            .Include(p => p.Relaciones)
            .Include(p => p.Documentos)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<bool> ExisteCodigoCatastralAsync(string codigoCatastral, CancellationToken ct = default)
    {
        var voResult = CodigoCatastral.Crear(codigoCatastral);
        if (voResult.IsFailure) return false;

        // EF Core no puede traducir operator== del ValueObject a SQL.
        // SqlQuery<bool> con FormattableString evita el pipeline de entidades
        // y garantiza que el parámetro @p0 se envíe correctamente a PostgreSQL.
        var codigoStr = voResult.Value.Valor;
        return await db.Database
            .SqlQuery<bool>(
                $"SELECT EXISTS(SELECT 1 FROM dominio.predios WHERE codigo_catastral = {codigoStr} AND NOT is_deleted) AS \"Value\"")
            .FirstAsync(ct);
    }

    public async Task<PagedResult<Predio>> ListarAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = db.Predios.AsNoTracking();

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Predio>(items, total, page, pageSize);
    }

    public void Agregar(Predio predio) => db.Predios.Add(predio);

    public async Task GuardarCambiosAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);
}
