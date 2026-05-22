using Microsoft.EntityFrameworkCore;
using SG.Application.Abstractions.Catastro;
using SG.Application.Common;
using SG.Domain.Catastro;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Catastro;

internal sealed class PropietarioRepositorio(ApplicationDbContext db) : IPropietarioRepositorio
{
    public async Task<Propietario?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default) =>
        await db.Propietarios.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<bool> ExisteCedulaAsync(string cedula, CancellationToken ct = default) =>
        await db.Propietarios.AnyAsync(p => p.Cedula == cedula, ct);

    public async Task<bool> ExisteNitAsync(string nit, CancellationToken ct = default) =>
        await db.Propietarios.AnyAsync(p => p.Nit == nit, ct);

    public async Task<PagedResult<Propietario>> ListarAsync(
        int page, int pageSize, string? busqueda, CancellationToken ct = default)
    {
        var query = db.Propietarios.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(busqueda))
        {
            var patron = $"%{busqueda.Trim()}%";
            query = query.Where(p =>
                (p.Nombre != null && EF.Functions.ILike(p.Nombre, patron)) ||
                (p.Apellidos != null && EF.Functions.ILike(p.Apellidos, patron)) ||
                (p.Cedula != null && EF.Functions.ILike(p.Cedula, patron)) ||
                (p.RazonSocial != null && EF.Functions.ILike(p.RazonSocial, patron)) ||
                (p.Nit != null && EF.Functions.ILike(p.Nit, patron)));
        }

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Propietario>(items, total, page, pageSize);
    }

    public void Agregar(Propietario propietario) => db.Propietarios.Add(propietario);

    public async Task GuardarCambiosAsync(CancellationToken ct = default) =>
        await db.SaveChangesAsync(ct);
}
