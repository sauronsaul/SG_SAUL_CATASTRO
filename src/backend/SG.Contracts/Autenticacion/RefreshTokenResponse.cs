namespace SG.Contracts.Autenticacion;

public sealed record RefreshTokenResponse(
    string AccessToken,
    DateTime ExpiresAt,
    string RefreshToken);
