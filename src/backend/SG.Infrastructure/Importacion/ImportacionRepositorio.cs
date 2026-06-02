using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.Importacion;
using SG.Application.Common;
using SG.Infrastructure.Persistencia;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Infrastructure.Importacion;

internal sealed class ImportacionRepositorio(ApplicationDbContext db)
    : IImportacionRepositorio
{
    // ── Consultas (AsNoTracking) ───────────────────────────────────────────

    public async Task<PagedResult<ImportacionDomain.Importacion>> ListarAsync(
        int page,
        int pageSize,
        ImportacionDomain.EstadoImportacion? estado = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        CancellationToken ct = default)
    {
        var query = db.Importaciones.AsNoTracking();

        if (estado.HasValue)
            query = query.Where(i => i.Estado == estado.Value);

        if (fechaDesde.HasValue)
            query = query.Where(i => i.FechaImportacion >= fechaDesde.Value);

        if (fechaHasta.HasValue)
            query = query.Where(i => i.FechaImportacion <= fechaHasta.Value);

        query = query.OrderByDescending(i => i.FechaImportacion);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ImportacionDomain.Importacion>(items, total, page, pageSize);
    }

    public async Task<ImportacionDomain.Importacion?> ObtenerPorIdSinTrackingAsync(
        Guid id, CancellationToken ct = default) =>
        await db.Importaciones
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct);

    // ── Escritura (con tracking) ───────────────────────────────────────────

    public async Task<ImportacionDomain.Importacion?> ObtenerPorIdAsync(
        Guid id, CancellationToken ct = default) =>
        await db.Importaciones.FirstOrDefaultAsync(i => i.Id == id, ct);

    public void Agregar(ImportacionDomain.Importacion importacion) =>
        db.Importaciones.Add(importacion);

    public async Task GuardarCambiosAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);
}
