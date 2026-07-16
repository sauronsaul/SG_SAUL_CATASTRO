using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.Catastro;
using SG.Application.Common;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;
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

    public Task<bool> ExisteTripleteCatastralAsync(
        string municipioCodigo,
        int codUv,
        int codMan,
        int codPred,
        CancellationToken ct = default) =>
        db.Predios.AnyAsync(
            p => p.MunicipioCodigo == municipioCodigo &&
                 p.CodUv == codUv && p.CodMan == codMan && p.CodPred == codPred,
            ct);

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

    public async Task<IReadOnlyDictionary<(string Zona, string Manzana, string Lote), EstadoPredio>>
        ObtenerEstadosPorTripletasAsync(
            string municipioCodigo,
            IReadOnlyCollection<(string Zona, string Manzana, string Lote)> tripletas,
            CancellationToken ct = default)
    {
        if (tripletas.Count == 0)
            return new Dictionary<(string, string, string), EstadoPredio>();

        // Consulta con IN sobre cada componente; el filtro exacto por tripleta completa
        // se aplica en memoria para evitar generar SQL con predicados de tupla no portables.
        var zonas = tripletas.Select(t => t.Zona).Distinct().ToList();
        var manzanas = tripletas.Select(t => t.Manzana).Distinct().ToList();
        var lotes = tripletas.Select(t => t.Lote).Distinct().ToList();
        var tripletaSet = tripletas.ToHashSet();

        var filas = await db.Predios
            .AsNoTracking()
            .Where(p => p.MunicipioCodigo == municipioCodigo
                     && zonas.Contains(p.Ubicacion.Zona)
                     && manzanas.Contains(p.Ubicacion.Manzana)
                     && lotes.Contains(p.Ubicacion.Lote))
            .Select(p => new
            {
                p.Ubicacion.Zona,
                p.Ubicacion.Manzana,
                p.Ubicacion.Lote,
                p.Estado,
            })
            .ToListAsync(ct);

        return filas
            .Where(f => tripletaSet.Contains((f.Zona, f.Manzana, f.Lote)))
            .ToDictionary(
                f => (f.Zona, f.Manzana, f.Lote),
                f => f.Estado);
    }

    public async Task<Dictionary<(string Zona, string Manzana, string Lote), Predio>>
        ObtenerParaActualizarPorTripletasAsync(
            string municipioCodigo,
            IReadOnlyCollection<(string Zona, string Manzana, string Lote)> tripletas,
            CancellationToken ct = default)
    {
        if (tripletas.Count == 0)
            return new Dictionary<(string, string, string), Predio>();

        var zonas = tripletas.Select(t => t.Zona).Distinct().ToList();
        var manzanas = tripletas.Select(t => t.Manzana).Distinct().ToList();
        var lotes = tripletas.Select(t => t.Lote).Distinct().ToList();
        var tripletaSet = tripletas.ToHashSet();

        // Sin AsNoTracking: EF Core trackea las entidades para que
        // ActualizarDesdeImportacion y AgregarConstruccion se persistan en SaveChangesAsync.
        var prediosList = await db.Predios
            .Where(p => p.MunicipioCodigo == municipioCodigo
                     && zonas.Contains(p.Ubicacion.Zona)
                     && manzanas.Contains(p.Ubicacion.Manzana)
                     && lotes.Contains(p.Ubicacion.Lote))
            .ToListAsync(ct);

        return prediosList
            .Where(p => tripletaSet.Contains((p.Ubicacion.Zona, p.Ubicacion.Manzana, p.Ubicacion.Lote)))
            .ToDictionary(p => (p.Ubicacion.Zona, p.Ubicacion.Manzana, p.Ubicacion.Lote));
    }
}
