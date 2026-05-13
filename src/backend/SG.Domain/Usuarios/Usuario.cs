using SG.Domain.Common;

namespace SG.Domain.Usuarios;

public sealed class Usuario : AggregateRoot
{
    public string Email { get; private set; } = string.Empty;
    public string EmailNormalizado { get; private set; } = string.Empty;
    public string NombreCompleto { get; private set; } = string.Empty;

    private Usuario() { }

    public static Result<Usuario> Crear(string? email, string? nombreCompleto)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Result.Failure<Usuario>(UsuarioErrores.EmailRequerido);

        var emailTrimmed = email.Trim();
        if (!EsEmailValido(emailTrimmed))
            return Result.Failure<Usuario>(UsuarioErrores.EmailInvalido);

        if (string.IsNullOrWhiteSpace(nombreCompleto))
            return Result.Failure<Usuario>(UsuarioErrores.NombreVacio);

        if (emailTrimmed.Length > 320)
            return Result.Failure<Usuario>(UsuarioErrores.EmailInvalido);

        if (nombreCompleto.Trim().Length > 200)
            return Result.Failure<Usuario>(UsuarioErrores.NombreVacio);

        var usuario = new Usuario
        {
            Email = emailTrimmed,
            EmailNormalizado = emailTrimmed.ToUpperInvariant(),
            NombreCompleto = nombreCompleto.Trim()
        };

        return Result.Success(usuario);
    }

    private static bool EsEmailValido(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0 || at == email.Length - 1) return false;
        var domain = email[(at + 1)..];
        return domain.Contains('.') && !domain.StartsWith('.') && !domain.EndsWith('.');
    }
}
