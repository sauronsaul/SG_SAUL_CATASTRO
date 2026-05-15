using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SG.Application.Abstractions.Autenticacion;
using SG.Infrastructure.Identidad;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Seguridad;

internal sealed class RefreshTokenRepositorio(
    ApplicationDbContext db,
    IConfiguration configuration)
    : IRefreshTokenRepositorio
{
    public async Task<RefreshTokenActivoDto?> BuscarPorTokenAsync(string token, CancellationToken ct = default)
    {
        var rt = await db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Token == token, ct);

        if (rt is null) return null;

        var estaRevocado = rt.RevokedAt != null;
        var estaExpirado = DateTime.UtcNow >= rt.ExpiresAt;

        return new RefreshTokenActivoDto(
            rt.Id,
            rt.UsuarioId,
            IsActive: !estaRevocado && !estaExpirado,
            EstaRevocado: estaRevocado,
            EstaExpirado: estaExpirado,
            ReplacedByToken: rt.ReplacedByToken);
    }

    public async Task CrearAsync(Guid usuarioId, string tokenString, string createdByIp, CancellationToken ct = default)
    {
        var expiryDays = int.TryParse(configuration["Jwt:RefreshExpiryDays"], out var d) ? d : 7;

        var rt = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Token = tokenString,
            ExpiresAt = DateTime.UtcNow.AddDays(expiryDays),
            CreatedAt = DateTime.UtcNow,
            CreatedByIp = createdByIp,
        };

        db.RefreshTokens.Add(rt);
        await db.SaveChangesAsync(ct);
    }

    public async Task RevocarAsync(Guid id, string revokedByIp, string? replacedByToken, string? reason, CancellationToken ct = default)
    {
        var rt = await db.RefreshTokens.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rt is null) return;

        rt.RevokedAt = DateTime.UtcNow;
        rt.RevokedByIp = revokedByIp;
        rt.ReplacedByToken = replacedByToken;
        rt.RevokedReason = reason;

        await db.SaveChangesAsync(ct);
    }

    public async Task RevocarTodosAsync(Guid usuarioId, string revokedByIp, string reason, CancellationToken ct = default)
    {
        var activos = await db.RefreshTokens
            .Where(r => r.UsuarioId == usuarioId && r.RevokedAt == null && r.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        if (activos.Count == 0) return;

        var now = DateTime.UtcNow;
        foreach (var rt in activos)
        {
            rt.RevokedAt = now;
            rt.RevokedByIp = revokedByIp;
            rt.RevokedReason = reason;
        }

        await db.SaveChangesAsync(ct);
    }
}
