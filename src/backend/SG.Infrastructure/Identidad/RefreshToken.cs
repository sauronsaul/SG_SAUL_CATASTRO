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

    public bool IsActive => RevokedAt == null && DateTime.UtcNow < ExpiresAt;

    public UsuarioIdentidad Usuario { get; set; } = null!;
}
