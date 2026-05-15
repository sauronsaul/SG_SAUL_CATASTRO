namespace SG.Application.Abstractions.Autenticacion;

public interface IRefreshTokenRepositorio
{
    Task<RefreshTokenActivoDto?> BuscarPorTokenAsync(string token, CancellationToken ct = default);
    Task CrearAsync(Guid usuarioId, string tokenString, string createdByIp, CancellationToken ct = default);
    Task RevocarAsync(Guid id, string revokedByIp, string? replacedByToken, string? reason, CancellationToken ct = default);
    Task RevocarTodosAsync(Guid usuarioId, string revokedByIp, string reason, CancellationToken ct = default);
}
