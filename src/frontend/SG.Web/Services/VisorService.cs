using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SG.Contracts.GIS;

namespace SG.Web.Services;

public sealed class VisorService(HttpClient httpClient, SesionAutenticacion sesion)
{
    public async Task<ResultadoVisor<IReadOnlyList<MunicipioVisorDto>>> ListarMunicipiosAsync(
        CancellationToken ct = default) =>
        await ObtenerAsync<IReadOnlyList<MunicipioVisorDto>>("api/visor/municipios", ct);

    public async Task<ResultadoVisor<ConfiguracionVisorDto>> ObtenerConfiguracionAsync(
        string municipioCodigo,
        CancellationToken ct = default) =>
        await ObtenerAsync<ConfiguracionVisorDto>(
            $"api/visor/{Uri.EscapeDataString(municipioCodigo)}/configuracion",
            ct);

    private async Task<ResultadoVisor<T>> ObtenerAsync<T>(string ruta, CancellationToken ct)
    {
        if (sesion.AccessToken is null)
            return ResultadoVisor<T>.NoAutorizado();

        using var request = new HttpRequestMessage(HttpMethod.Get, ruta);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sesion.AccessToken);
        using var response = await httpClient.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            sesion.NotificarNoAutorizado();
            return ResultadoVisor<T>.NoAutorizado();
        }

        if (!response.IsSuccessStatusCode)
            return ResultadoVisor<T>.Fallo("No se pudo cargar la configuración municipal del visor.");

        var valor = await response.Content.ReadFromJsonAsync<T>(ct);
        return valor is null
            ? ResultadoVisor<T>.Fallo("La API devolvió una configuración vacía.")
            : ResultadoVisor<T>.Exitoso(valor);
    }
}

public sealed record ResultadoVisor<T>(T? Valor, bool EsNoAutorizado, string? Error)
{
    public bool EsExitoso => Valor is not null && Error is null && !EsNoAutorizado;
    public static ResultadoVisor<T> Exitoso(T valor) => new(valor, false, null);
    public static ResultadoVisor<T> NoAutorizado() => new(default, true, null);
    public static ResultadoVisor<T> Fallo(string error) => new(default, false, error);
}
