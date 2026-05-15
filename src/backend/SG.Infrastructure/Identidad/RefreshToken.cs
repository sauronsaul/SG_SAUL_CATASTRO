namespace SG.Infrastructure.Identidad;

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedByIp { get; set; } = string.Empty;
    public string? RevokedByIp { get; set; }
    public string? RevokedReason { get; set; }

    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;

    // TODO Sprint 2: EliminarUsuarioCommand debe revocar explícitamente los tokens
    // activos del usuario antes de aplicar IsDeleted = true (ver ADR 0025).
    public UsuarioIdentidad Usuario { get; set; } = null!;
}
