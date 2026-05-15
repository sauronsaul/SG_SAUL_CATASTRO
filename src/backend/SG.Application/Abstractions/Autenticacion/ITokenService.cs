namespace SG.Application.Abstractions.Autenticacion;

public interface ITokenService
{
    (string AccessToken, DateTime ExpiresAt) GenerarAccessToken(
        Guid userId,
        string email,
        string nombreCompleto,
        IReadOnlyList<string> roles);

    string GenerarRefreshToken();
}
