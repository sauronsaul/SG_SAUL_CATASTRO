using System.Buffers.Binary;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using ProjNet.CoordinateSystems;
using SG.Api.IntegrationTests.Infrastructure;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Autenticacion;
using SG.Contracts.Importacion;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;
using Xunit.Abstractions;

namespace SG.Api.IntegrationTests.Importacion;

[Collection("Postgres")]
public sealed class ImportacionVersionadaE2ETests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly SgApiFactory _factory;
    private readonly HttpClient _clientAdmin;
    private readonly ITestOutputHelper _output;

    public ImportacionVersionadaE2ETests(PostgreSqlFixture fixture, ITestOutputHelper output)
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
    public async Task PostVersion_PaqueteValido_EncolaYPollingLlegaAPreviewListo()
    {
        var paquete = CrearPaqueteSieteCapas(corromperEdificaciones: false);

        var response = await PostPaqueteAsync(paquete, "uyuni-prueba.zip");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        response.Headers.Location!.AbsolutePath.Should().MatchRegex(@"^/api/importaciones/versiones/[0-9a-f-]+$");
        _output.WriteLine($"POST status={(int)response.StatusCode} Location={response.Headers.Location}");
        var creada = await response.Content.ReadFromJsonAsync<CrearVersionImportacionDto>(JsonOpts);
        creada.Should().NotBeNull();
        creada!.Estado.Should().Be("EnCarga");

        var estado = await EsperarEstadoAsync(creada.DatasetVersionId, "PreviewListo");

        estado.ReportePreliminar.CapaEnCurso.Should().BeNull();
        estado.ReportePreliminar.CapasCompletadas.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["capa_parcelas"] = 2,
            ["capa_edificaciones"] = 2,
            ["capa_predios_no_fotografiados"] = 2,
            ["capa_manzanas"] = 2,
            ["capa_distritos"] = 2,
            ["capa_zonas"] = 2,
            ["capa_vias"] = 2,
        });
        estado.ErrorCarga.Should().BeNull();
        _output.WriteLine($"GET PreviewListo: {JsonSerializer.Serialize(estado)}");
    }

    [Fact]
    public async Task PostVersion_GeometriasAuxiliaresReales_CargaYReportaO4()
    {
        var paquete = CrearPaqueteSieteCapas(
            corromperEdificaciones: false,
            escenarioGeometria: EscenarioGeometria.AuxiliaresReales);

        var response = await PostPaqueteAsync(paquete, "uyuni-geometrias-reales.zip");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var creada = (await response.Content.ReadFromJsonAsync<CrearVersionImportacionDto>(JsonOpts))!;
        var estado = await EsperarEstadoAsync(creada.DatasetVersionId, "PreviewListo");

        estado.ReportePreliminar.CapasCompletadas.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["capa_parcelas"] = 2,
            ["capa_edificaciones"] = 2,
            ["capa_predios_no_fotografiados"] = 2,
            ["capa_manzanas"] = 2,
            ["capa_distritos"] = 2,
            ["capa_zonas"] = 2,
            ["capa_vias"] = 3,
        });
        var observaciones = estado.ReportePreliminar.Validacion!.Observaciones;
        observaciones.Should().HaveCount(2);
        observaciones.Should().OnlyContain(x => x.Codigo == "O4");

        var edificaciones = observaciones.Single(x => x.Capa == "capa_edificaciones");
        edificaciones.Conteo.Should().Be(1);
        edificaciones.Ejemplos.Single().FilaOrigen.Should().Be(2);
        edificaciones.Ejemplos.Single().Identificadores.Should().Contain(
            new KeyValuePair<string, string?>("cod_uv", "1"));
        edificaciones.Ejemplos.Single().Identificadores.Should().Contain(
            new KeyValuePair<string, string?>("cod_man", "2"));
        edificaciones.Ejemplos.Single().Identificadores.Should().Contain(
            new KeyValuePair<string, string?>("cod_pred", "2"));

        var vias = observaciones.Single(x => x.Capa == "capa_vias");
        vias.Conteo.Should().Be(1);
        vias.Ejemplos.Single().FilaOrigen.Should().Be(2);
        vias.Ejemplos.Single().Identificadores["nombre"].Should().NotBeNullOrWhiteSpace();
        vias.Ejemplos.Single().Identificadores["material"].Should().Be("Asfalto");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var filasEdificaciones = await db.CapasEdificaciones.AsNoTracking()
            .Where(x => x.DatasetVersionId == creada.DatasetVersionId)
            .OrderBy(x => x.FilaOrigen)
            .ToListAsync();
        filasEdificaciones[0].Geometria!.NumGeometries.Should().Be(1);
        (filasEdificaciones[1].Geometria is null).Should().BeTrue();

        var filasManzanas = await db.CapasManzanas.AsNoTracking()
            .Where(x => x.DatasetVersionId == creada.DatasetVersionId)
            .OrderBy(x => x.FilaOrigen)
            .ToListAsync();
        filasManzanas[1].Geometria!.NumGeometries.Should().Be(2);

        var filasVias = await db.CapasVias.AsNoTracking()
            .Where(x => x.DatasetVersionId == creada.DatasetVersionId)
            .OrderBy(x => x.FilaOrigen)
            .ToListAsync();
        filasVias[0].Geometria!.NumGeometries.Should().Be(1);
        (filasVias[1].Geometria is null).Should().BeTrue();
        filasVias[2].Geometria!.NumGeometries.Should().Be(2);

        _output.WriteLine($"REPORTE O4: {JsonSerializer.Serialize(estado.ReportePreliminar)}");
    }

    [Fact]
    public async Task PostVersion_GeometriasInvalidasRecuperables_SeparaO1DeNulosGenuinosO4()
    {
        var paquete = CrearPaqueteSieteCapas(
            corromperEdificaciones: false,
            escenarioGeometria: EscenarioGeometria.InvalidasRecuperablesConNulosGenuinos);

        var response = await PostPaqueteAsync(paquete, "uyuni-invalidas-recuperables.zip");

        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var creada = (await response.Content.ReadFromJsonAsync<CrearVersionImportacionDto>(JsonOpts))!;
        var estado = await EsperarEstadoAsync(creada.DatasetVersionId, "PreviewListo");

        estado.ReportePreliminar.CapasCompletadas.Should().BeEquivalentTo(new Dictionary<string, int>
        {
            ["capa_parcelas"] = 2,
            ["capa_edificaciones"] = 3,
            ["capa_predios_no_fotografiados"] = 2,
            ["capa_manzanas"] = 2,
            ["capa_distritos"] = 2,
            ["capa_zonas"] = 2,
            ["capa_vias"] = 2,
        });

        var invalidas = estado.ReportePreliminar.Validacion!.GeometriasInvalidas;
        invalidas.Should().HaveCount(2);
        invalidas.Single(x => x.Capa == "capa_edificaciones").Should().Match<GeometriasInvalidasCapaDto>(x =>
            x.Conteo == 1 && x.Ejemplos.Single().FilaOrigen == 2 &&
            !string.IsNullOrWhiteSpace(x.Ejemplos.Single().Razon));
        invalidas.Single(x => x.Capa == "capa_manzanas").Should().Match<GeometriasInvalidasCapaDto>(x =>
            x.Conteo == 1 && x.Ejemplos.Single().FilaOrigen == 2 &&
            !string.IsNullOrWhiteSpace(x.Ejemplos.Single().Razon));

        var observaciones = estado.ReportePreliminar.Validacion.Observaciones;
        observaciones.Should().HaveCount(2);
        observaciones.Should().OnlyContain(x => x.Codigo == "O4");
        observaciones.Single(x => x.Capa == "capa_edificaciones").Should().Match<ObservacionPreviewVersionDto>(x =>
            x.Conteo == 1 && x.Ejemplos.Single().FilaOrigen == 3);
        observaciones.Single(x => x.Capa == "capa_vias").Should().Match<ObservacionPreviewVersionDto>(x =>
            x.Conteo == 1 && x.Ejemplos.Single().FilaOrigen == 2);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var edificaciones = await db.CapasEdificaciones.AsNoTracking()
            .Where(x => x.DatasetVersionId == creada.DatasetVersionId)
            .OrderBy(x => x.FilaOrigen)
            .ToListAsync();
        (edificaciones[1].Geometria is not null).Should().BeTrue();
        edificaciones[1].Geometria!.IsValid.Should().BeFalse();
        (edificaciones[2].Geometria is null).Should().BeTrue();

        var manzanas = await db.CapasManzanas.AsNoTracking()
            .Where(x => x.DatasetVersionId == creada.DatasetVersionId)
            .OrderBy(x => x.FilaOrigen)
            .ToListAsync();
        (manzanas[1].Geometria is not null).Should().BeTrue();
        manzanas[1].Geometria!.IsValid.Should().BeFalse();

        var vias = await db.CapasVias.AsNoTracking()
            .Where(x => x.DatasetVersionId == creada.DatasetVersionId)
            .OrderBy(x => x.FilaOrigen)
            .ToListAsync();
        (vias[1].Geometria is null).Should().BeTrue();

        _output.WriteLine($"REPORTE O1/O4: {JsonSerializer.Serialize(estado.ReportePreliminar)}");
    }

    [Fact]
    public async Task PostVersion_ParcelaNula_MarcaFallidaConMensajePreciso()
    {
        var paquete = CrearPaqueteSieteCapas(
            corromperEdificaciones: false,
            escenarioGeometria: EscenarioGeometria.ParcelaNula);

        var response = await PostPaqueteAsync(paquete, "uyuni-parcela-nula.zip");
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var creada = (await response.Content.ReadFromJsonAsync<CrearVersionImportacionDto>(JsonOpts))!;
        var estado = await EsperarEstadoAsync(creada.DatasetVersionId, "Fallida");

        estado.ErrorCarga.Should().Be(
            "Capa 'capa_parcelas', fila 2: geometría nula; se esperaba Polygon (se admite MultiPolygon de una sola parte).");
        _output.WriteLine($"ERROR PARCELA NULA: {JsonSerializer.Serialize(estado)}");
    }

    [Fact]
    public async Task PostVersion_ParcelaMultiPolygon_MarcaFallidaConMensajePreciso()
    {
        var paquete = CrearPaqueteSieteCapas(
            corromperEdificaciones: false,
            escenarioGeometria: EscenarioGeometria.ParcelaMultiParte);

        var response = await PostPaqueteAsync(paquete, "uyuni-parcela-multiparte.zip");
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var creada = (await response.Content.ReadFromJsonAsync<CrearVersionImportacionDto>(JsonOpts))!;
        var estado = await EsperarEstadoAsync(creada.DatasetVersionId, "Fallida");

        estado.ErrorCarga.Should().Be(
            "Capa 'capa_parcelas', fila 2: llegó MultiPolygon de 2 partes; se esperaba Polygon (se admite MultiPolygon de una sola parte).");
        _output.WriteLine($"ERROR PARCELA MULTIPARTE: {JsonSerializer.Serialize(estado)}");
    }

    [Fact]
    public async Task ActivarVersion_FlujoCompleto_RetornaResumenYProtegeCapasActivas()
    {
        var paquete = CrearPaqueteSieteCapas(corromperEdificaciones: false);
        var post = await PostPaqueteAsync(paquete, "uyuni-activar.zip");
        var creada = (await post.Content.ReadFromJsonAsync<CrearVersionImportacionDto>(JsonOpts))!;
        var preview = await EsperarEstadoAsync(creada.DatasetVersionId, "PreviewListo");

        preview.ReportePreliminar.Validacion.Should().NotBeNull();
        preview.ReportePreliminar.Validacion!.Bloqueantes.Should().BeEmpty();
        _output.WriteLine($"REPORTE PREVIEW: {JsonSerializer.Serialize(preview.ReportePreliminar)}");

        var activar = await _clientAdmin.PostAsync(
            $"/api/importaciones/versiones/{creada.DatasetVersionId}/activar",
            null);
        var respuesta = await activar.Content.ReadAsStringAsync();

        activar.StatusCode.Should().Be(HttpStatusCode.OK, respuesta);
        var activada = JsonSerializer.Deserialize<ActivarVersionImportacionDto>(respuesta, JsonOpts)!;
        activada.Estado.Should().Be("Activa");
        activada.Resumen.Altas.Should().Be(2);
        _output.WriteLine($"POST ACTIVAR status={(int)activar.StatusCode}: {respuesta}");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.DatasetVersiones.CountAsync(x =>
            x.MunicipioCodigo == "UYUNI" && x.Estado == EstadoDatasetVersion.Activa))
            .Should().Be(1);
        (await db.Predios.CountAsync(x => x.UltimaVersionVistaId == creada.DatasetVersionId))
            .Should().Be(2);

        var act = async () => await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE dominio.capa_parcelas
            SET codigo_geografico = 'TAMPERED'
            WHERE dataset_version_id = {creada.DatasetVersionId}
            """);
        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*UPDATE*prohibida*Activa*");
    }

    [Fact]
    public async Task PostVersion_ShpCorrupto_MarcaFallidaYPurgaLasCapas()
    {
        var paquete = CrearPaqueteSieteCapas(corromperEdificaciones: true);

        var response = await PostPaqueteAsync(paquete, "uyuni-corrupto.zip");
        response.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var creada = (await response.Content.ReadFromJsonAsync<CrearVersionImportacionDto>(JsonOpts))!;

        var estado = await EsperarEstadoAsync(creada.DatasetVersionId, "Fallida");
        estado.ErrorCarga.Should().NotBeNullOrWhiteSpace();
        _output.WriteLine($"GET Fallida: {JsonSerializer.Serialize(estado)}");

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var filas = await db.Database.SqlQuery<int>($@"
SELECT (
    (SELECT COUNT(*) FROM dominio.capa_parcelas WHERE dataset_version_id = {creada.DatasetVersionId}) +
    (SELECT COUNT(*) FROM dominio.capa_edificaciones WHERE dataset_version_id = {creada.DatasetVersionId}) +
    (SELECT COUNT(*) FROM dominio.capa_predios_no_fotografiados WHERE dataset_version_id = {creada.DatasetVersionId}) +
    (SELECT COUNT(*) FROM dominio.capa_manzanas WHERE dataset_version_id = {creada.DatasetVersionId}) +
    (SELECT COUNT(*) FROM dominio.capa_distritos WHERE dataset_version_id = {creada.DatasetVersionId}) +
    (SELECT COUNT(*) FROM dominio.capa_zonas WHERE dataset_version_id = {creada.DatasetVersionId}) +
    (SELECT COUNT(*) FROM dominio.capa_vias WHERE dataset_version_id = {creada.DatasetVersionId})
)::int AS ""Value""").SingleAsync();

        filas.Should().Be(0);
        _output.WriteLine($"SQL filas de capas de versión Fallida={filas}");
    }

    [Fact]
    public async Task PostVersion_SinPaquete_Retorna400SinCrearVersionFallida()
    {
        var versionesAntes = await ContarVersionesAsync();
        using var content = new MultipartFormDataContent();

        var response = await _clientAdmin.PostAsync("/api/importaciones/versiones", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ContarVersionesAsync()).Should().Be(versionesAntes);
    }

    [Fact]
    public async Task PostVersion_ZipSinArchivoEsperado_Retorna400SinCrearVersionFallida()
    {
        var versionesAntes = await ContarVersionesAsync();
        var paquete = CrearPaqueteSieteCapas(
            corromperEdificaciones: false,
            omitirPrjDe: TipoCapa.Vias);

        var response = await PostPaqueteAsync(paquete, "uyuni-incompleto.zip");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ContarVersionesAsync()).Should().Be(versionesAntes);
    }

    [Fact]
    public async Task Arranque_MarcaVersionEnCargaHuerfanaComoFallida()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var version = DatasetVersion.Crear(999, "HUERFANA", null, "Prueba de reinicio", "paquete/prueba.zip");
        db.DatasetVersiones.Add(version);
        await db.SaveChangesAsync();

        var servicio = scope.ServiceProvider.GetRequiredService<ICargaVersionadaServicio>();
        await servicio.MarcarHuerfanasAlArrancarAsync();
        db.ChangeTracker.Clear();

        var huerfana = await db.DatasetVersiones.SingleAsync(x => x.Id == version.Id);
        huerfana.Estado.Should().Be(EstadoDatasetVersion.Fallida);
        huerfana.ErrorCarga.Should().Be("carga interrumpida por reinicio");
        _output.WriteLine($"Huérfana: estado={huerfana.Estado}, error={huerfana.ErrorCarga}");
    }

    private async Task<HttpResponseMessage> PostPaqueteAsync(byte[] paquete, string nombre)
    {
        using var content = new MultipartFormDataContent();
        var archivo = new ByteArrayContent(paquete);
        archivo.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        content.Add(archivo, "paquete", nombre);
        return await _clientAdmin.PostAsync("/api/importaciones/versiones", content);
    }

    private async Task<int> ContarVersionesAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.DatasetVersiones.CountAsync();
    }

    private async Task<EstadoVersionImportacionDto> EsperarEstadoAsync(Guid versionId, string estadoEsperado)
    {
        for (var intento = 0; intento < 100; intento++)
        {
            var response = await _clientAdmin.GetAsync($"/api/importaciones/versiones/{versionId}");
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var estado = (await response.Content.ReadFromJsonAsync<EstadoVersionImportacionDto>(JsonOpts))!;
            if (estado.Estado == estadoEsperado)
                return estado;
            if (estado.Estado == "Fallida")
                throw new InvalidOperationException($"La carga falló: {estado.ErrorCarga}");

            await Task.Delay(100);
        }

        throw new TimeoutException($"La versión {versionId} no llegó a {estadoEsperado}.");
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

    internal static byte[] CrearPaqueteSieteCapas(
        bool corromperEdificaciones,
        TipoCapa? omitirPrjDe = null,
        EscenarioGeometria escenarioGeometria = EscenarioGeometria.Normal)
    {
        var directorio = Path.Combine(Path.GetTempPath(), $"sg_shp_test_{Guid.NewGuid():N}");
        var zip = Path.Combine(Path.GetTempPath(), $"sg_shp_test_{Guid.NewGuid():N}.zip");
        Directory.CreateDirectory(directorio);
        try
        {
            foreach (var definicion in DefinicionesCapasVersionadasUyuni.Todas)
            {
                CrearShapefile(directorio, definicion, corromperEdificaciones, escenarioGeometria);
                if (definicion.TipoCapa == omitirPrjDe)
                    File.Delete(Path.ChangeExtension(
                        Path.Combine(directorio, definicion.NombreArchivoShp),
                        ".prj"));
            }

            ZipFile.CreateFromDirectory(directorio, zip, CompressionLevel.NoCompression, includeBaseDirectory: false);
            return File.ReadAllBytes(zip);
        }
        finally
        {
            if (File.Exists(zip))
                File.Delete(zip);
            if (Directory.Exists(directorio))
                Directory.Delete(directorio, recursive: true);
        }
    }

    private static void CrearShapefile(
        string directorio,
        DefinicionCapaVersionadaUyuni definicion,
        bool corromperEdificaciones,
        EscenarioGeometria escenarioGeometria)
    {
        var rutaShp = Path.Combine(directorio, definicion.NombreArchivoShp);
        var geometrias = CrearGeometrias(definicion.TipoCapa, escenarioGeometria);
        var features = geometrias
            .Select((geometria, indice) => new Feature(
                geometria ?? (definicion.TipoCapa == TipoCapa.Vias
                    ? CrearLinea(indice + 1)
                    : CrearPoligono(indice + 1)),
                CrearAtributos(definicion.TipoCapa, indice + 1)))
            .ToList();
        Shapefile.WriteAllFeatures(features, rutaShp);
        foreach (var indiceNulo in geometrias
                     .Select((geometria, indice) => (geometria, indice))
                     .Where(x => x.geometria is null)
                     .Select(x => x.indice))
            MarcarGeometriaNula(rutaShp, indiceNulo);
        File.WriteAllText(Path.ChangeExtension(rutaShp, ".prj"), ProjectedCoordinateSystem.WGS84_UTM(19, false).WKT);

        if (corromperEdificaciones && definicion.TipoCapa == TipoCapa.Construcciones)
            File.WriteAllBytes(rutaShp, [0x00, 0x01, 0x02]);
    }

    private static void MarcarGeometriaNula(string rutaShp, int indiceCeroBased)
    {
        var rutaShx = Path.ChangeExtension(rutaShp, ".shx");
        Span<byte> offsetBytes = stackalloc byte[4];
        using (var shx = File.OpenRead(rutaShx))
        {
            shx.Position = 100 + indiceCeroBased * 8L;
            shx.ReadExactly(offsetBytes);
        }

        var offsetPalabras = BinaryPrimitives.ReadInt32BigEndian(offsetBytes);
        using var shp = File.Open(rutaShp, FileMode.Open, FileAccess.Write, FileShare.None);
        shp.Position = offsetPalabras * 2L + 8;
        shp.Write([0, 0, 0, 0]); // Shape Type 0 = Null Shape; conserva intactos SHX y DBF.
    }

    private static IReadOnlyList<Geometry?> CrearGeometrias(
        TipoCapa tipoCapa,
        EscenarioGeometria escenario) => (tipoCapa, escenario) switch
    {
        (TipoCapa.Construcciones, EscenarioGeometria.AuxiliaresReales) =>
            [CrearPoligono(1), null],
        (TipoCapa.Manzanas, EscenarioGeometria.AuxiliaresReales) =>
            [CrearPoligono(1), CrearMultiPoligono(2)],
        (TipoCapa.Vias, EscenarioGeometria.AuxiliaresReales) =>
            [CrearLinea(1), null, CrearMultiLinea(3)],
        (TipoCapa.Construcciones, EscenarioGeometria.InvalidasRecuperablesConNulosGenuinos) =>
            [CrearPoligono(1), CrearPoligonoAutoIntersectado(2), null],
        (TipoCapa.Manzanas, EscenarioGeometria.InvalidasRecuperablesConNulosGenuinos) =>
            [CrearPoligono(1), CrearPoligonoAutoIntersectado(2)],
        (TipoCapa.Vias, EscenarioGeometria.InvalidasRecuperablesConNulosGenuinos) =>
            [CrearLinea(1), null],
        (TipoCapa.Predios, EscenarioGeometria.ParcelaNula) =>
            [CrearPoligono(1), null],
        (TipoCapa.Predios, EscenarioGeometria.ParcelaMultiParte) =>
            [CrearPoligono(1), CrearMultiPoligono(2)],
        (TipoCapa.Vias, _) => [CrearLinea(1), CrearLinea(2)],
        _ => [CrearPoligono(1), CrearPoligono(2)],
    };

    private static Polygon CrearPoligono(int indice)
    {
        var x = 500000 + indice * 100;
        var y = 8000000 + indice * 100;
        return new Polygon(new LinearRing(
        [
            new Coordinate(x, y), new Coordinate(x + 10, y), new Coordinate(x + 10, y + 10),
            new Coordinate(x, y + 10), new Coordinate(x, y),
        ])) { SRID = 32719 };
    }

    private static LineString CrearLinea(int indice) => new(
        [new Coordinate(500000 + indice, 8000000), new Coordinate(500100 + indice, 8000100)]) { SRID = 32719 };

    private static Polygon CrearPoligonoAutoIntersectado(int indice)
    {
        var x = 500000 + indice * 100;
        var y = 8000000 + indice * 100;
        return new Polygon(new LinearRing(
        [
            new Coordinate(x, y), new Coordinate(x + 10, y + 10),
            new Coordinate(x + 10, y), new Coordinate(x, y + 10),
            new Coordinate(x, y),
        ])) { SRID = 32719 };
    }

    private static MultiPolygon CrearMultiPoligono(int indice) => new(
        [CrearPoligono(indice), CrearPoligono(indice + 100)]) { SRID = 32719 };

    private static MultiLineString CrearMultiLinea(int indice) => new(
        [CrearLinea(indice), CrearLinea(indice + 100)]) { SRID = 32719 };

    private static AttributesTable CrearAtributos(TipoCapa capa, int indice) => capa switch
    {
        TipoCapa.Predios => new AttributesTable
        {
            { "cod_uv", 1L }, { "cod_man", 2L }, { "cod_pred", (long)indice },
            { "cod_geo", $"0102{indice:000}" }, { "superficie", 100d }, { "nompro", "****" },
        },
        TipoCapa.Construcciones => new AttributesTable
        {
            { "id_edif", (long)indice }, { "cod_geo", $"0102{indice:000}" }, { "cod_uv", 1L },
            { "cod_man", 2L }, { "cod_pred", (long)indice }, { "edi_num", 1L }, { "edi_piso", 1L }, { "edi_are", 50d },
        },
        TipoCapa.PrediosNoFotografiados => new AttributesTable
        {
            { "id_predio", (long)indice }, { "cod_geo", $"0102{indice:000}" }, { "cod_uv", 1L }, { "cod_man", 2L }, { "cod_pred", (long)indice },
        },
        TipoCapa.Manzanas => new AttributesTable { { "cod_geo", "010200001" }, { "cod_uv", 1L }, { "cod_man", 2L }, { "Coordenada", 1d } },
        TipoCapa.Distritos => new AttributesTable { { "cod_geo", "01020000001" }, { "cod_uv", 1L }, { "nombre", "Distrito prueba" } },
        TipoCapa.ZonasValuacion => new AttributesTable { { "zona", "Zona prueba" }, { "id_zona", 1L }, { "cod_geo", "01020000001" } },
        TipoCapa.Vias => new AttributesTable { { "MATERIAL", "Asfalto" }, { "NOMBRE", "Vía prueba" }, { "TIPO", "Local" }, { "Distancia", 100d } },
        _ => throw new InvalidOperationException("Tipo de capa no soportado."),
    };

    internal enum EscenarioGeometria
    {
        Normal,
        AuxiliaresReales,
        InvalidasRecuperablesConNulosGenuinos,
        ParcelaNula,
        ParcelaMultiParte,
    }
}
