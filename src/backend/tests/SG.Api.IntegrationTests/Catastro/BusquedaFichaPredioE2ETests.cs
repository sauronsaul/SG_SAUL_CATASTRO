using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SG.Api.IntegrationTests.Importacion;
using SG.Api.IntegrationTests.Infrastructure;
using SG.Contracts.Autenticacion;
using SG.Contracts.Catastro;
using SG.Contracts.Importacion;
using SG.Infrastructure.Persistencia;
using Xunit.Abstractions;

namespace SG.Api.IntegrationTests.Catastro;

[Collection("Postgres")]
public sealed class BusquedaFichaPredioE2ETests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly SgApiFactory _factory;
    private readonly HttpClient _clientAdmin;
    private readonly ITestOutputHelper _output;

    public BusquedaFichaPredioE2ETests(PostgreSqlFixture fixture, ITestOutputHelper output)
    {
        _factory = new SgApiFactory(fixture.ConnectionString);
        _clientAdmin = CrearClienteAutenticado(LoginAdminAsync().GetAwaiter().GetResult());
        _output = output;
    }

    public void Dispose()
    {
        _clientAdmin.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Buscar_ValidaCriterio_DevuelveFichaPostgisYResuelveNuevaVersionActiva()
    {
        await LimpiarDatosCreadosAsync();
        try
        {
            using var anonimo = _factory.CreateClient();
            var sinAutenticacion = await anonimo.GetAsync(
                "/api/predios/buscar?distrito=1&manzana=2&predio=1");
            sinAutenticacion.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var criterioInvalido = await _clientAdmin.GetAsync(
                "/api/predios/buscar?distrito=0&manzana=2&predio=1");
            criterioInvalido.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            var primeraVersion = await ImportarYActivarAsync("ficha-primera.zip");
            var primeraRespuesta = await _clientAdmin.GetAsync(
                "/api/predios/buscar?distrito=1&manzana=2&predio=1");
            var primerCuerpo = await primeraRespuesta.Content.ReadAsStringAsync();
            primeraRespuesta.StatusCode.Should().Be(HttpStatusCode.OK, primerCuerpo);
            var primeraFicha = JsonSerializer.Deserialize<FichaPredioDto>(primerCuerpo, JsonOpts)!;

            primeraFicha.DatasetVersionId.Should().Be(primeraVersion.DatasetVersionId);
            primeraFicha.NumeroVersion.Should().Be(primeraVersion.NumeroVersion);
            primeraFicha.MunicipioCodigo.Should().Be("051201");
            primeraFicha.FilaOrigen.Should().Be(1);
            primeraFicha.Distrito.Should().Be(1);
            primeraFicha.Manzana.Should().Be(2);
            primeraFicha.Predio.Should().Be(1);
            primeraFicha.CodigoGeografico.Should().Be("0102001");
            primeraFicha.Estado.Should().Be("Importado");
            primeraFicha.SuperficieDeclaradaM2.Should().Be(100m);
            primeraFicha.SuperficieGraficaM2.Should().Be(100m);
            primeraFicha.PropietarioReferencia.Should().Be("****");
            primeraFicha.GeometriaPlanar.Srid.Should().Be(32719);
            primeraFicha.GeometriaPlanar.Tipo.Should().Be("Polygon");
            primeraFicha.GeometriaPlanar.Coordenadas.Should().ContainSingle();
            primeraFicha.GeometriaPlanar.Coordenadas[0].Should().HaveCountGreaterThanOrEqualTo(4);
            primeraFicha.GeometriaPlanar.Coordenadas[0][0]
                .Should().Equal(primeraFicha.GeometriaPlanar.Coordenadas[0][^1]);
            primeraFicha.Limites.Oeste.Should().BeLessThan(primeraFicha.Limites.Este);
            primeraFicha.Limites.Sur.Should().BeLessThan(primeraFicha.Limites.Norte);

            var distritoFueraDeUyuni = await _clientAdmin.GetAsync(
                "/api/predios/buscar?distrito=7&manzana=2&predio=1");
            distritoFueraDeUyuni.StatusCode.Should().Be(HttpStatusCode.NotFound);

            var segundaVersion = await ImportarYActivarAsync("ficha-segunda.zip");
            var segundaFicha = await _clientAdmin.GetFromJsonAsync<FichaPredioDto>(
                "/api/predios/buscar?distrito=1&manzana=2&predio=1",
                JsonOpts);

            segundaFicha.Should().NotBeNull();
            segundaFicha!.DatasetVersionId.Should().Be(segundaVersion.DatasetVersionId);
            segundaFicha.DatasetVersionId.Should().NotBe(primeraFicha.DatasetVersionId);
            segundaFicha.NumeroVersion.Should().Be(segundaVersion.NumeroVersion);

            _output.WriteLine(
                $"FICHA status={(int)primeraRespuesta.StatusCode} " +
                $"triplete={primeraFicha.Distrito}/{primeraFicha.Manzana}/{primeraFicha.Predio} " +
                $"fila={primeraFicha.FilaOrigen} declarada={primeraFicha.SuperficieDeclaradaM2:F4} " +
                $"grafica={primeraFicha.SuperficieGraficaM2:F4} " +
                $"geometria_srid={primeraFicha.GeometriaPlanar.Srid} " +
                $"anillos={primeraFicha.GeometriaPlanar.Coordenadas.Length} " +
                $"bbox={primeraFicha.Limites.Oeste},{primeraFicha.Limites.Sur}," +
                $"{primeraFicha.Limites.Este},{primeraFicha.Limites.Norte}");
            _output.WriteLine(
                $"VERSION ACTIVA antes={primeraFicha.DatasetVersionId} " +
                $"despues={segundaFicha.DatasetVersionId}; " +
                $"anonimo={(int)sinAutenticacion.StatusCode}; " +
                $"invalido={(int)criterioInvalido.StatusCode}; " +
                $"distrito7={(int)distritoFueraDeUyuni.StatusCode}");
        }
        finally
        {
            await LimpiarDatosCreadosAsync();
        }
    }

    private async Task<(Guid DatasetVersionId, int NumeroVersion)> ImportarYActivarAsync(string nombre)
    {
        var paquete = ImportacionVersionadaE2ETests.CrearPaqueteSieteCapas(
            corromperEdificaciones: false);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("051201"), "municipio_codigo");
        var archivo = new ByteArrayContent(paquete);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(archivo, "paquete", nombre);

        var post = await _clientAdmin.PostAsync("/api/importaciones/versiones", content);
        var cuerpo = await post.Content.ReadAsStringAsync();
        post.StatusCode.Should().Be(HttpStatusCode.Accepted, cuerpo);
        var creada = JsonSerializer.Deserialize<CrearVersionImportacionDto>(cuerpo, JsonOpts)!;
        var preview = await EsperarPreviewAsync(creada.DatasetVersionId);

        var activar = await _clientAdmin.PostAsync(
            $"/api/importaciones/versiones/{creada.DatasetVersionId}/activar",
            null);
        var respuestaActivar = await activar.Content.ReadAsStringAsync();
        activar.StatusCode.Should().Be(HttpStatusCode.OK, respuestaActivar);
        return (creada.DatasetVersionId, preview.NumeroVersion);
    }

    private async Task<EstadoVersionImportacionDto> EsperarPreviewAsync(Guid versionId)
    {
        for (var intento = 0; intento < 100; intento++)
        {
            var response = await _clientAdmin.GetAsync($"/api/importaciones/versiones/{versionId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var estado = (await response.Content.ReadFromJsonAsync<EstadoVersionImportacionDto>(JsonOpts))!;
            if (estado.Estado == "PreviewListo")
                return estado;
            if (estado.Estado == "Fallida")
                throw new InvalidOperationException($"La carga fallo: {estado.ErrorCarga}");

            await Task.Delay(100);
        }

        throw new TimeoutException($"La version {versionId} no llego a PreviewListo.");
    }

    private async Task LimpiarDatosCreadosAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM dominio.predios;
            UPDATE dominio.dataset_versiones
            SET estado = 'Descartada'
            WHERE municipio_codigo = '051201';
            DELETE FROM dominio.capa_parcelas
            WHERE dataset_version_id IN (SELECT id FROM dominio.dataset_versiones WHERE municipio_codigo = '051201');
            DELETE FROM dominio.capa_edificaciones
            WHERE dataset_version_id IN (SELECT id FROM dominio.dataset_versiones WHERE municipio_codigo = '051201');
            DELETE FROM dominio.capa_predios_no_fotografiados
            WHERE dataset_version_id IN (SELECT id FROM dominio.dataset_versiones WHERE municipio_codigo = '051201');
            DELETE FROM dominio.capa_manzanas
            WHERE dataset_version_id IN (SELECT id FROM dominio.dataset_versiones WHERE municipio_codigo = '051201');
            DELETE FROM dominio.capa_distritos
            WHERE dataset_version_id IN (SELECT id FROM dominio.dataset_versiones WHERE municipio_codigo = '051201');
            DELETE FROM dominio.capa_zonas
            WHERE dataset_version_id IN (SELECT id FROM dominio.dataset_versiones WHERE municipio_codigo = '051201');
            DELETE FROM dominio.capa_vias
            WHERE dataset_version_id IN (SELECT id FROM dominio.dataset_versiones WHERE municipio_codigo = '051201');
            DELETE FROM dominio.dataset_versiones
            WHERE municipio_codigo = '051201';
            """);

        var versionesRestantes = await db.DatasetVersiones.CountAsync(x => x.MunicipioCodigo == "051201");
        var prediosRestantes = await db.Predios.IgnoreQueryFilters().CountAsync();
        versionesRestantes.Should().Be(0);
        prediosRestantes.Should().Be(0);
    }

    private async Task<string> LoginAdminAsync()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest(PostgreSqlFixture.AdminEmail, PostgreSqlFixture.AdminPassword));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOpts))!.AccessToken;
    }

    private HttpClient CrearClienteAutenticado(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
