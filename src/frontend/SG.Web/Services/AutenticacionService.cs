using System.Net.Http.Json;
using SG.Contracts.Autenticacion;

namespace SG.Web.Services;

public sealed class AutenticacionService(HttpClient httpClient, SesionAutenticacion sesion)
{
    public async Task<ResultadoAutenticacion> IniciarSesionAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/login",
            new LoginRequest(email, password),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return ResultadoAutenticacion.Fallo("Correo o contraseña incorrectos.");

        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken);
        if (login is null)
            return ResultadoAutenticacion.Fallo("La API devolvió una respuesta de autenticación vacía.");

        sesion.Iniciar(login);
        return ResultadoAutenticacion.Exito();
    }
}

public sealed record ResultadoAutenticacion(bool EsExitoso, string? Error)
{
    public static ResultadoAutenticacion Exito() => new(true, null);
    public static ResultadoAutenticacion Fallo(string error) => new(false, error);
}
