using SG.Domain.Common;

namespace SG.Application.Autenticacion;

public static class AutenticacionErrores
{
    public static readonly DomainError CredencialesInvalidas =
        new("Auth.CredencialesInvalidas", "Credenciales inválidas.");

    public static readonly DomainError CuentaBloqueada =
        new("Auth.CuentaBloqueada", "La cuenta está bloqueada temporalmente. Intente en 15 minutos.");

    public static readonly DomainError TokenInvalido =
        new("Auth.TokenInvalido", "El token proporcionado no es válido.");

    public static readonly DomainError TokenExpirado =
        new("Auth.TokenExpirado", "El token ha expirado.");

    public static readonly DomainError ReutilizacionDetectada =
        new("Auth.ReutilizacionDetectada", "Uso inválido del token. Todos los tokens han sido revocados por seguridad.");

    public static readonly DomainError UsuarioNoEncontrado =
        new("Auth.UsuarioNoEncontrado", "Usuario no encontrado.");
}
