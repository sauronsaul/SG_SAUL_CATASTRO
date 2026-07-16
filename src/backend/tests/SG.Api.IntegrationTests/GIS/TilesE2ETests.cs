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
using SG.Contracts.Importacion;
using SG.Infrastructure.Persistencia;
using Xunit.Abstractions;

namespace SG.Api.IntegrationTests.GIS;

[Collection("Postgres")]
public sealed class TilesE2ETests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly SgApiFactory _factory;
    private readonly HttpClient _clientAdmin;
    private readonly ITestOutputHelper _output;

    public TilesE2ETests(PostgreSqlFixture fixture, ITestOutputHelper output)
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
    public async Task GetTile_RutaComplejaLiga_DecodificaO1_VacioYActivacionCambiaETag()
    {
        try
        {
            var primeraVersion = await ImportarYActivarAsync(
                ImportacionVersionadaE2ETests.EscenarioGeometria.InvalidasRecuperablesConNulosGenuinos,
                "tiles-o1.zip");
            var coordenadas = await ObtenerCoordenadasTileAsync(primeraVersion);
            var ruta = $"/api/tiles/edificaciones/16/{coordenadas.X}/{coordenadas.Y}.mvt";

            // Primera llamada MVT del test: prueba el complex segment {y:int}.mvt.
            var response = await _clientAdmin.GetAsync(ruta);
            var contenido = await response.Content.ReadAsByteArrayAsync();

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            response.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.mapbox-vector-tile");
            response.Headers.CacheControl!.Private.Should().BeTrue();
            response.Headers.CacheControl.NoCache.Should().BeTrue();
            response.Headers.Vary.Should().Contain("Authorization");
            var primerEtag = response.Headers.ETag!.Tag;
            var capas = MvtDecoderPruebas.Decodificar(contenido);
            capas.Should().ContainSingle();
            capas[0].Nombre.Should().Be("edificaciones");
            capas[0].FeatureIds.Should().BeEquivalentTo([1UL, 2UL]);
            capas[0].Claves.Should().Contain(["cod_uv", "cod_man", "cod_pred"]);
            _output.WriteLine(
                $"RUTA MVT status={(int)response.StatusCode} ruta={ruta} bytes={contenido.Length} " +
                $"etag={primerEtag} capa={capas[0].Nombre} ids={string.Join(',', capas[0].FeatureIds)}");

            var capasEsperadas = new Dictionary<string, (int Features, string[] Claves)>
            {
                ["parcelas"] = (2, ["cod_uv", "cod_man", "cod_pred"]),
                ["predios-no-fotografiados"] = (2, ["cod_uv", "cod_man", "cod_pred"]),
                ["manzanas"] = (2, ["cod_uv", "cod_man"]),
                ["distritos"] = (2, ["cod_uv", "nombre"]),
                ["zonas"] = (2, ["nombre_zona"]),
                ["vias"] = (1, ["nombre", "tipo", "material"]),
            };
            foreach (var (nombreCapa, esperado) in capasEsperadas)
            {
                var respuestaCapa = await _clientAdmin.GetAsync(
                    $"/api/tiles/{nombreCapa}/16/{coordenadas.X}/{coordenadas.Y}.mvt");
                respuestaCapa.StatusCode.Should().Be(HttpStatusCode.OK, nombreCapa);
                var capaDecodificada = MvtDecoderPruebas.Decodificar(
                    await respuestaCapa.Content.ReadAsByteArrayAsync()).Should().ContainSingle().Subject;
                capaDecodificada.Nombre.Should().Be(nombreCapa);
                capaDecodificada.FeatureIds.Should().HaveCount(esperado.Features);
                capaDecodificada.Claves.Should().Contain(esperado.Claves);
                _output.WriteLine(
                    $"CAPA {nombreCapa} features={capaDecodificada.FeatureIds.Count} " +
                    $"claves={string.Join(',', capaDecodificada.Claves)}");
            }

            using var condicional = new HttpRequestMessage(HttpMethod.Get, ruta);
            condicional.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(primerEtag));
            var noModificado = await _clientAdmin.SendAsync(condicional);
            noModificado.StatusCode.Should().Be(HttpStatusCode.NotModified);

            var vacio = await _clientAdmin.GetAsync("/api/tiles/edificaciones/22/0/0.mvt");
            vacio.StatusCode.Should().Be(HttpStatusCode.NoContent);
            vacio.Headers.ETag.Should().NotBeNull();

            var coordenadasInvalidas = await _clientAdmin.GetAsync("/api/tiles/edificaciones/23/0/0.mvt");
            coordenadasInvalidas.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var capaDesconocida = await _clientAdmin.GetAsync("/api/tiles/capa_edificaciones/16/0/0.mvt");
            capaDesconocida.StatusCode.Should().Be(HttpStatusCode.NotFound);

            await ImportarYActivarAsync(
                ImportacionVersionadaE2ETests.EscenarioGeometria.Normal,
                "tiles-version-nueva.zip");
            var segundaRespuesta = await _clientAdmin.GetAsync(ruta);
            segundaRespuesta.StatusCode.Should().Be(HttpStatusCode.OK);
            var segundoEtag = segundaRespuesta.Headers.ETag!.Tag;
            segundoEtag.Should().NotBe(primerEtag);
            _output.WriteLine(
                $"ETAG ACTIVACION antes={primerEtag} despues={segundoEtag}; " +
                $"vacio={(int)vacio.StatusCode}; coordenadasInvalidas={(int)coordenadasInvalidas.StatusCode}; " +
                $"capaDesconocida={(int)capaDesconocida.StatusCode}");

            using var anonimo = _factory.CreateClient();
            var sinAutenticacion = await anonimo.GetAsync(ruta);
            sinAutenticacion.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            await LimpiarDatosCreadosAsync();
        }
    }

    private async Task<Guid> ImportarYActivarAsync(
        ImportacionVersionadaE2ETests.EscenarioGeometria escenario,
        string nombre)
    {
        var paquete = ImportacionVersionadaE2ETests.CrearPaqueteSieteCapas(
            corromperEdificaciones: false,
            escenarioGeometria: escenario);
        using var content = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(paquete);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(archivo, "paquete", nombre);

        var post = await _clientAdmin.PostAsync("/api/importaciones/versiones", content);
        var cuerpo = await post.Content.ReadAsStringAsync();
        post.StatusCode.Should().Be(HttpStatusCode.Accepted, cuerpo);
        var creada = JsonSerializer.Deserialize<CrearVersionImportacionDto>(cuerpo, JsonOpts)!;
        await EsperarPreviewAsync(creada.DatasetVersionId);

        var activar = await _clientAdmin.PostAsync(
            $"/api/importaciones/versiones/{creada.DatasetVersionId}/activar",
            null);
        var respuestaActivar = await activar.Content.ReadAsStringAsync();
        activar.StatusCode.Should().Be(HttpStatusCode.OK, respuestaActivar);
        _output.WriteLine(
            $"ACTIVAR fixture={nombre} version={creada.DatasetVersionId} respuesta={respuestaActivar}");
        return creada.DatasetVersionId;
    }

    private async Task EsperarPreviewAsync(Guid versionId)
    {
        for (var intento = 0; intento < 100; intento++)
        {
            var response = await _clientAdmin.GetAsync($"/api/importaciones/versiones/{versionId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var estado = (await response.Content.ReadFromJsonAsync<EstadoVersionImportacionDto>(JsonOpts))!;
            if (estado.Estado == "PreviewListo")
                return;
            if (estado.Estado == "Fallida")
                throw new InvalidOperationException($"La carga fallo: {estado.ErrorCarga}");

            await Task.Delay(100);
        }

        throw new TimeoutException($"La version {versionId} no llego a PreviewListo.");
    }

    private async Task<TileCoordenadasSql> ObtenerCoordenadasTileAsync(Guid versionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Database.SqlQuery<TileCoordenadasSql>($"""
            SELECT floor(
                       (ST_X(ST_Transform(ST_PointOnSurface(geometria), 3857)) + 20037508.342789244)
                       / 40075016.685578488 * 65536
                   )::int AS x,
                   floor(
                       (20037508.342789244 - ST_Y(ST_Transform(ST_PointOnSurface(geometria), 3857)))
                       / 40075016.685578488 * 65536
                   )::int AS y
            FROM dominio.capa_edificaciones
            WHERE dataset_version_id = {versionId} AND fila_origen = 1
            """).SingleAsync();
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
        if (versionesRestantes != 0 || prediosRestantes != 0)
            throw new InvalidOperationException(
                $"Limpieza E2E incompleta: versiones={versionesRestantes}, predios={prediosRestantes}.");

        _output.WriteLine("LIMPIEZA E2E versiones=0 predios=0");
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

    private sealed record TileCoordenadasSql(int X, int Y);
}
