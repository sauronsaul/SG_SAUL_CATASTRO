using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SG.Api.IntegrationTests.Infrastructure;
using SG.Contracts.Autenticacion;
using SG.Contracts.Importacion;
using SG.Domain.Catalogos;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;
using Xunit.Abstractions;

namespace SG.Api.IntegrationTests.Importacion;

[Collection("Postgres")]
public sealed class ImportacionVersionadaCaranaviE2ETests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly SgApiFactory _factory;
    private readonly HttpClient _clientAdmin;
    private readonly ITestOutputHelper _output;

    public ImportacionVersionadaCaranaviE2ETests(PostgreSqlFixture fixture, ITestOutputHelper output)
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
    public async Task PostVersion_MunicipioInexistente_Retorna404()
    {
        var response = await PostPaqueteAsync(
            ImportacionVersionadaE2ETests.CrearPaqueteTresCapasCaranavi(),
            "999999",
            "caranavi-inexistente.zip");
        var cuerpo = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, cuerpo);
        cuerpo.Should().Contain("999999");
        _output.WriteLine($"MUNICIPIO INEXISTENTE status={(int)response.StatusCode}: {cuerpo}");
    }

    [Fact]
    public async Task PostVersion_MunicipioSinEsquema_Retorna422()
    {
        const string municipioSinEsquema = "099999";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!await db.Municipios.AnyAsync(x => x.CodigoIne == municipioSinEsquema))
            {
                db.Municipios.Add(Municipio.Crear(
                    municipioSinEsquema,
                    "Municipio prueba sin esquema",
                    "Gobierno Autónomo Municipal de Prueba",
                    "La Paz",
                    "Fixture de integración").Value);
                await db.SaveChangesAsync();
            }
        }

        var response = await PostPaqueteAsync(
            ImportacionVersionadaE2ETests.CrearPaqueteTresCapasCaranavi(),
            municipioSinEsquema,
            "sin-esquema.zip");
        var cuerpo = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity, cuerpo);
        cuerpo.Should().Contain("no tiene un esquema de capas");
        _output.WriteLine($"MUNICIPIO SIN ESQUEMA status={(int)response.StatusCode}: {cuerpo}");
    }

    [Fact]
    public async Task Caranavi_TresCapas_PreviewYActivacionSinParcelasConservanUyuni()
    {
        var uyuniPost = await PostPaqueteAsync(
            ImportacionVersionadaE2ETests.CrearPaqueteSieteCapas(corromperEdificaciones: false),
            "051201",
            "uyuni-base-caranavi.zip");
        var uyuniCuerpo = await uyuniPost.Content.ReadAsStringAsync();
        uyuniPost.StatusCode.Should().Be(HttpStatusCode.Accepted, uyuniCuerpo);
        var uyuni = JsonSerializer.Deserialize<CrearVersionImportacionDto>(uyuniCuerpo, JsonOpts)!;
        await EsperarEstadoAsync(uyuni.DatasetVersionId, "PreviewListo");
        var uyuniActivacion = await _clientAdmin.PostAsync(
            $"/api/importaciones/versiones/{uyuni.DatasetVersionId}/activar",
            null);
        uyuniActivacion.StatusCode.Should().Be(HttpStatusCode.OK, await uyuniActivacion.Content.ReadAsStringAsync());

        var prediosUyuniAntes = await ContarPrediosAsync("051201");
        prediosUyuniAntes.Should().BeGreaterThan(0);

        var caranaviPost = await PostPaqueteAsync(
            ImportacionVersionadaE2ETests.CrearPaqueteTresCapasCaranavi(),
            "022001",
            "caranavi-tres-capas.zip");
        var caranaviCuerpo = await caranaviPost.Content.ReadAsStringAsync();
        caranaviPost.StatusCode.Should().Be(HttpStatusCode.Accepted, caranaviCuerpo);
        var caranavi = JsonSerializer.Deserialize<CrearVersionImportacionDto>(caranaviCuerpo, JsonOpts)!;
        var preview = await EsperarEstadoAsync(caranavi.DatasetVersionId, "PreviewListo");

        preview.MunicipioCodigo.Should().Be("022001");
        preview.ReportePreliminar.CapasCompletadas.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["capa_manzanas"] = 2,
            ["capa_areas_urbanas"] = 2,
            ["capa_puntos_geodesicos"] = 2,
        });
        preview.ReportePreliminar.Validacion!.Bloqueantes.Should().NotContain(x => x.Codigo == "B3");
        preview.ReportePreliminar.Validacion.GeometriasInvalidas.Should().OnlyContain(x => x.Codigo == "O1");
        preview.ReportePreliminar.Validacion.GeometriasInvalidas.Select(x => x.Capa)
            .Should().Contain(["capa_manzanas", "capa_areas_urbanas"]);
        preview.ReportePreliminar.Validacion.Observaciones.Should().ContainSingle(x =>
            x.Codigo == "O4" && x.Capa == "capa_puntos_geodesicos" && x.Conteo == 1);
        preview.ReportePreliminar.Validacion.ProyeccionReconciliacion.Omitida.Should().BeTrue();
        preview.ReportePreliminar.Validacion.ProyeccionReconciliacion.MotivoOmision
            .Should().Be("Esquema municipal sin capa de predios.");
        preview.ReportePreliminar.Validacion.EsquemaEvaluado!.MunicipioCodigo.Should().Be("022001");
        preview.ReportePreliminar.Validacion.EsquemaEvaluado.Capas.Should().HaveCount(3);

        await VerificarFilasCaranaviAsync(caranavi.DatasetVersionId);

        var activar = await _clientAdmin.PostAsync(
            $"/api/importaciones/versiones/{caranavi.DatasetVersionId}/activar",
            null);
        var activarCuerpo = await activar.Content.ReadAsStringAsync();
        activar.StatusCode.Should().Be(HttpStatusCode.OK, activarCuerpo);
        var activacion = JsonSerializer.Deserialize<ActivarVersionImportacionDto>(activarCuerpo, JsonOpts)!;

        activacion.Resumen.Should().Be(new ResumenReconciliacionDto(
            0,
            0,
            0,
            0,
            true,
            "Esquema municipal sin capa de predios."));
        (await ContarPrediosAsync("051201")).Should().Be(prediosUyuniAntes);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var activas = await db.DatasetVersiones.AsNoTracking()
            .Where(x => x.Estado == EstadoDatasetVersion.Activa &&
                        (x.MunicipioCodigo == "051201" || x.MunicipioCodigo == "022001"))
            .GroupBy(x => x.MunicipioCodigo)
            .Select(x => new { Municipio = x.Key, Conteo = x.Count() })
            .ToListAsync();
        activas.Should().BeEquivalentTo([
            new { Municipio = "051201", Conteo = 1 },
            new { Municipio = "022001", Conteo = 1 },
        ]);

        _output.WriteLine($"PREVIEW CARANAVI: {JsonSerializer.Serialize(preview)}");
        _output.WriteLine($"ACTIVACION CARANAVI: {activarCuerpo}");
        _output.WriteLine($"PREDIOS 051201 antes={prediosUyuniAntes}, despues={await ContarPrediosAsync("051201")}");
        _output.WriteLine($"ACTIVAS: {JsonSerializer.Serialize(activas)}");
    }

    private async Task VerificarFilasCaranaviAsync(Guid versionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var manzana = await db.CapasManzanas.AsNoTracking()
            .Where(x => x.DatasetVersionId == versionId)
            .OrderBy(x => x.FilaOrigen)
            .FirstAsync();
        var area = await db.CapasAreasUrbanas.AsNoTracking()
            .Where(x => x.DatasetVersionId == versionId)
            .OrderBy(x => x.FilaOrigen)
            .FirstAsync();
        var punto = await db.CapasPuntosGeodesicos.AsNoTracking()
            .Where(x => x.DatasetVersionId == versionId)
            .OrderBy(x => x.FilaOrigen)
            .FirstAsync();

        manzana.CodMan.Should().Be(101);
        manzana.AtributosExtra.Should().Contain("Layer").And.NotContain("No_MANZANO");
        area.AtributosExtra.Should().Contain("Layer").And.Contain("AREA_URBANA");
        punto.AtributosExtra.Should().Contain("PUNTOS").And.Contain("ESTE").And.Contain("NORTE");
    }

    private async Task<HttpResponseMessage> PostPaqueteAsync(
        byte[] paquete,
        string municipioCodigo,
        string nombre)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(municipioCodigo), "municipio_codigo");
        var archivo = new ByteArrayContent(paquete);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(archivo, "paquete", nombre);
        return await _clientAdmin.PostAsync("/api/importaciones/versiones", content);
    }

    private async Task<EstadoVersionImportacionDto> EsperarEstadoAsync(Guid versionId, string estadoEsperado)
    {
        for (var intento = 0; intento < 300; intento++)
        {
            var response = await _clientAdmin.GetAsync($"/api/importaciones/versiones/{versionId}");
            response.EnsureSuccessStatusCode();
            var estado = (await response.Content.ReadFromJsonAsync<EstadoVersionImportacionDto>(JsonOpts))!;
            if (estado.Estado == estadoEsperado)
                return estado;
            if (estado.Estado == "Fallida")
                throw new InvalidOperationException($"La carga falló: {estado.ErrorCarga}");
            await Task.Delay(100);
        }

        throw new TimeoutException($"La versión {versionId} no llegó a {estadoEsperado}.");
    }

    private async Task<int> ContarPrediosAsync(string municipioCodigo)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Predios.CountAsync(x => x.MunicipioCodigo == municipioCodigo);
    }

    private async Task<string> LoginAdminAsync()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(
            PostgreSqlFixture.AdminEmail,
            PostgreSqlFixture.AdminPassword));
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
