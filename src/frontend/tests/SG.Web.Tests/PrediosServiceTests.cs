using System.Net;
using System.Text;
using FluentAssertions;
using SG.Contracts.Autenticacion;
using SG.Web.Models;
using SG.Web.Services;

namespace SG.Web.Tests;

public sealed class PrediosServiceTests
{
    [Fact]
    public async Task Buscar_SinSesion_NoEjecutaHttp()
    {
        var handler = new HandlerControlado(_ => throw new InvalidOperationException());
        var servicio = CrearServicio(handler, autenticado: false);

        var resultado = await servicio.BuscarAsync(new CriterioBusquedaPredio(1, 2, 3));

        resultado.Estado.Should().Be(EstadoConsultaPredio.NoAutorizado);
        handler.Solicitudes.Should().Be(0);
    }

    [Fact]
    public async Task Buscar_NoEncontrado_DevuelveEstadoYEnviaTripleteAutorizado()
    {
        HttpRequestMessage? solicitud = null;
        var handler = new HandlerControlado(request =>
        {
            solicitud = request;
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var servicio = CrearServicio(handler, autenticado: true);

        var resultado = await servicio.BuscarAsync(new CriterioBusquedaPredio(7, 20, 30));

        resultado.Estado.Should().Be(EstadoConsultaPredio.NoEncontrado);
        solicitud!.RequestUri!.PathAndQuery.Should().Be(
            "/api/predios/buscar?distrito=7&manzana=20&predio=30");
        solicitud.Headers.Authorization!.Scheme.Should().Be("Bearer");
        solicitud.Headers.Authorization.Parameter.Should().Be("access-token-prueba");
    }

    [Fact]
    public async Task Buscar_RespuestaValida_DeserializaFicha()
    {
        const string json =
            """
            {
              "predioId":"11111111-1111-1111-1111-111111111111",
              "datasetVersionId":"22222222-2222-2222-2222-222222222222",
              "numeroVersion":3,"municipioCodigo":"051201","filaOrigen":1,
              "distrito":1,"manzana":1,"predio":1,
              "codigoCatastral":"1-1-1","codigoGeografico":"010101",
              "estado":"Activo","superficieDeclaradaM2":100.0000,
              "superficieGraficaM2":100.0000,"superficieOficialM2":null,
              "propietarioReferencia":"PERSONA PRUEBA","tipoInmueble":"LOTE",
              "nombreVia":"CALLE PRUEBA","barrio":"CENTRO","direccion":"S/N",
              "usoTerreno":"VIVIENDA","topografiaTerreno":"PLANO",
              "servicioAgua":"SI","servicioLuz":"SI",
              "servicioAlcantarillado":"NO","servicioTelefonia":"NO",
              "geometriaPlanar":{"srid":32719,"tipo":"Polygon","coordenadas":[[[500000,7700000],[500010,7700000],[500010,7700010],[500000,7700000]]]},
              "limites":{"oeste":-67,"sur":-21,"este":-66,"norte":-20}
            }
            """;
        var handler = new HandlerControlado(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });
        var servicio = CrearServicio(handler, autenticado: true);

        var resultado = await servicio.BuscarAsync(new CriterioBusquedaPredio(1, 1, 1));

        resultado.Estado.Should().Be(EstadoConsultaPredio.Encontrado);
        resultado.Ficha.Should().NotBeNull();
        resultado.Ficha!.MunicipioCodigo.Should().Be("051201");
        resultado.Ficha.GeometriaPlanar.Srid.Should().Be(32719);
        resultado.Ficha.GeometriaPlanar.Coordenadas.Should().ContainSingle();
        resultado.Ficha.Limites.Oeste.Should().Be(-67);
    }

    private static PrediosService CrearServicio(HandlerControlado handler, bool autenticado)
    {
        var sesion = new SesionAutenticacion();
        if (autenticado)
        {
            sesion.Iniciar(new(
                "access-token-prueba",
                DateTime.UtcNow.AddMinutes(15),
                "refresh-token-no-persistido",
                new UsuarioDto(Guid.NewGuid(), "tecnico@uyuni.bo", "Tecnico", ["Tecnico"])));
        }

        return new PrediosService(
            new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") },
            sesion);
    }

    private sealed class HandlerControlado(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int Solicitudes { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Solicitudes++;
            return Task.FromResult(responder(request));
        }
    }
}
