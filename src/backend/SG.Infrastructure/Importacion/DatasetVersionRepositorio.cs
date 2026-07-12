using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.Importacion;
using SG.Infrastructure.Persistencia;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Infrastructure.Importacion;

internal sealed class DatasetVersionRepositorio(ApplicationDbContext db) : IDatasetVersionRepositorio
{
    public Task<ImportacionDomain.DatasetVersion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        db.DatasetVersiones.FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<ImportacionDomain.DatasetVersion?> ObtenerPorIdSinTrackingAsync(Guid id, CancellationToken ct = default) =>
        db.DatasetVersiones.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task<IReadOnlyList<ImportacionDomain.DatasetVersion>> ObtenerEnCargaAsync(CancellationToken ct = default) =>
        await db.DatasetVersiones
            .Where(x => x.Estado == ImportacionDomain.EstadoDatasetVersion.EnCarga)
            .ToListAsync(ct);

    public async Task<int> ObtenerSiguienteNumeroAsync(string municipioCodigo, CancellationToken ct = default) =>
        (await db.DatasetVersiones
            .Where(x => x.MunicipioCodigo == municipioCodigo)
            .Select(x => (int?)x.NumeroVersion)
            .MaxAsync(ct) ?? 0) + 1;

    public void Agregar(ImportacionDomain.DatasetVersion version) => db.DatasetVersiones.Add(version);

    public Task GuardarCambiosAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
