using System.Net;
using System.Text;
using FluentAssertions;
using SG.Contracts.Autenticacion;
using SG.Web.Services;

namespace SG.Web.Tests;

public sealed class VisorServiceTests
{
    [Fact]
    public async Task Configuracion_UsaMunicipioYBearer()
    {
        HttpRequestMessage? solicitud = null;
        var handler = new HandlerControlado(request =>
        {
            solicitud = request;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "municipio":{"codigo":"022001","nombre":"Caranavi","nombreOficial":"GAM Caranavi"},
                      "numeroVersionActiva":2,
                      "bbox":{"oeste":-67.58,"sur":-15.85,"este":-67.53,"norte":-15.82},
                      "capas":[],
                      "capacidades":{"tienePredios":false}
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });
        var servicio = CrearServicio(handler);

        var resultado = await servicio.ObtenerConfiguracionAsync("022001");

        resultado.EsExitoso.Should().BeTrue();
        resultado.Valor!.Municipio.Nombre.Should().Be("Caranavi");
        solicitud!.RequestUri!.AbsolutePath.Should().Be("/api/visor/022001/configuracion");
        solicitud.Headers.Authorization!.Parameter.Should().Be("access-token-prueba");
    }

    private static VisorService CrearServicio(HttpMessageHandler handler)
    {
        var sesion = new SesionAutenticacion();
        sesion.Iniciar(new(
            "access-token-prueba",
            DateTime.UtcNow.AddMinutes(15),
            "refresh-token",
            new UsuarioDto(Guid.NewGuid(), "tecnico@sg.bo", "Tecnico", ["Tecnico"])));
        return new VisorService(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") },
            sesion);
    }

    private sealed class HandlerControlado(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
