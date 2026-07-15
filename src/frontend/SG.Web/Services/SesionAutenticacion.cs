using SG.Contracts.Autenticacion;

namespace SG.Web.Services;

public sealed class SesionAutenticacion
{
    public string? AccessToken { get; private set; }
    public DateTime? ExpiresAt { get; private set; }
    public UsuarioDto? Usuario { get; private set; }
    public bool RequiereAutenticacion { get; private set; } = true;
    public bool SesionExpiro { get; private set; }

    public event Action? Cambio;

    public void Iniciar(LoginResponse respuesta)
    {
        AccessToken = respuesta.AccessToken;
        ExpiresAt = respuesta.ExpiresAt;
        Usuario = respuesta.Usuario;
        RequiereAutenticacion = false;
        SesionExpiro = false;
        Cambio?.Invoke();
    }

    public bool EstaExpirada(DateTime ahoraUtc) =>
        ExpiresAt is null || ExpiresAt.Value.ToUniversalTime() <= ahoraUtc.ToUniversalTime();

    public bool NotificarNoAutorizado()
    {
        if (RequiereAutenticacion)
            return false;

        AccessToken = null;
        ExpiresAt = null;
        Usuario = null;
        RequiereAutenticacion = true;
        SesionExpiro = true;
        Cambio?.Invoke();
        return true;
    }

    public void Cerrar()
    {
        AccessToken = null;
        ExpiresAt = null;
        Usuario = null;
        RequiereAutenticacion = true;
        SesionExpiro = false;
        Cambio?.Invoke();
    }
}
