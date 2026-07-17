using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using SG.Contracts.Catastro;
using SG.Web.Models;

namespace SG.Web.Services;

public sealed class PrediosService(HttpClient httpClient, SesionAutenticacion sesion)
{
    public async Task<ResultadoConsultaPredio> BuscarAsync(
        string municipioCodigo,
        CriterioBusquedaPredio criterio,
        CancellationToken cancellationToken = default)
    {
        if (sesion.AccessToken is null)
            return ResultadoConsultaPredio.NoAutorizado();

        var ruta =
            $"api/predios/{Uri.EscapeDataString(municipioCodigo)}/buscar?distrito={criterio.Distrito}" +
            $"&manzana={criterio.Manzana}&predio={criterio.Predio}";
        using var request = new HttpRequestMessage(HttpMethod.Get, ruta);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sesion.AccessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            sesion.NotificarNoAutorizado();
            return ResultadoConsultaPredio.NoAutorizado();
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
            return ResultadoConsultaPredio.NoEncontrado();

        if (!response.IsSuccessStatusCode)
            return ResultadoConsultaPredio.Fallo("No se pudo consultar la ficha del predio.");

        var ficha = await response.Content.ReadFromJsonAsync<FichaPredioDto>(cancellationToken);
        return ficha is null
            ? ResultadoConsultaPredio.Fallo("La API devolvio una ficha vacia.")
            : ResultadoConsultaPredio.Encontrado(ficha);
    }
}

public sealed record ResultadoConsultaPredio(
    EstadoConsultaPredio Estado,
    FichaPredioDto? Ficha,
    string? Error)
{
    public static ResultadoConsultaPredio Encontrado(FichaPredioDto ficha) =>
        new(EstadoConsultaPredio.Encontrado, ficha, null);

    public static ResultadoConsultaPredio NoEncontrado() =>
        new(
            EstadoConsultaPredio.NoEncontrado,
            null,
            "No se encontro un predio activo con ese distrito, manzana y predio.");

    public static ResultadoConsultaPredio NoAutorizado() =>
        new(EstadoConsultaPredio.NoAutorizado, null, null);

    public static ResultadoConsultaPredio Fallo(string error) =>
        new(EstadoConsultaPredio.Error, null, error);
}

public enum EstadoConsultaPredio
{
    Encontrado,
    NoEncontrado,
    NoAutorizado,
    Error,
}
