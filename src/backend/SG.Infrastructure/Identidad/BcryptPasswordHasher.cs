using BCrypt.Net;
using Microsoft.AspNetCore.Identity;

namespace SG.Infrastructure.Identidad;

public sealed class BcryptPasswordHasher : IPasswordHasher<UsuarioIdentidad>
{
    private const int WorkFactor = 12;

    public string HashPassword(UsuarioIdentidad user, string password)
        => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);

    public PasswordVerificationResult VerifyHashedPassword(
        UsuarioIdentidad user,
        string hashedPassword,
        string providedPassword)
        => BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword)
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
}
