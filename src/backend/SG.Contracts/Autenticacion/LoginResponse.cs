namespace SG.Contracts.Autenticacion;

public sealed record LoginResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken,
    UsuarioDto Usuario);
