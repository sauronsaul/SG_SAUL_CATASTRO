using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using SG.Api.IntegrationTests.Infrastructure;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Autenticacion;
using SG.Contracts.Importacion;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;
using SG.Domain.Catastro.ValueObjects;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;
using Xunit.Abstractions;

namespace SG.Api.IntegrationTests.Importacion;

[Collection("Postgres")]
public sealed class ActivacionReconciliacionE2ETests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    private readonly SgApiFactory _factory;
    private readonly HttpClient _clientAdmin;
    private readonly ITestOutputHelper _output;
    private readonly string _connectionString;

    public ActivacionReconciliacionE2ETests(PostgreSqlFixture fixture, ITestOutputHelper output)
    {
        _factory = new SgApiFactory(fixture.ConnectionString);
        _connectionString = fixture.ConnectionString;
        _clientAdmin = CrearClienteAutenticado(LoginAdminAsync().GetAwaiter().GetResult());
        _output = output;
    }

    public void Dispose()
    {
        _clientAdmin.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Activar_CasoControlado_ProduceResumenExactoYConservaDatosGenerados()
    {
        await LimpiarMaestroAsync();
        var usuarioId = await ObtenerAdminIdAsync();
        var predioSinCambio = CrearPredioMaestro(900, 1, 1, 100m, 100, "Sin cambio", usuarioId);
        var predioActualizar = CrearPredioMaestro(900, 1, 2, 100m, 200, "Anterior", usuarioId);
        var predioAusente = CrearPredioMaestro(900, 1, 3, 100m, 300, "Ausente", usuarioId);
        predioAusente.AgregarDocumento(
            "respaldo.pdf", "application/pdf", 10, "test/respaldo.pdf", TipoDocumento.Otro, usuarioId);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Predios.AddRange(predioSinCambio, predioActualizar, predioAusente);
            await db.SaveChangesAsync();
            _output.WriteLine($"SQL ANTES: {JsonSerializer.Serialize(await ConsultarMaestroAsync(db, 900))}");
        }

        var municipio = $"TB-{Guid.NewGuid():N}"[..12];
        var versionId = await CrearVersionPreviewAsync(municipio, 1,
        [
            new ParcelaPrueba(900, 1, 1, 100m, 100, "Sin cambio", Invalida: false),
            new ParcelaPrueba(900, 1, 2, 150m, 200, "Actualizado", Invalida: true),
            new ParcelaPrueba(900, 1, 4, 200m, 400, "Alta", Invalida: false),
        ]);
        var estadoPreview = await ObtenerEstadoAsync(versionId);
        estadoPreview.ReportePreliminar.Validacion!.GeometriasInvalidas
            .Single(x => x.Capa == "capa_parcelas").Conteo.Should().Be(1);
        estadoPreview.ReportePreliminar.Validacion.ProyeccionReconciliacion
            .PosibleRenumeracion.Should().BeTrue();

        var response = await _clientAdmin.PostAsync(
            $"/api/importaciones/versiones/{versionId}/activar",
            null);
        var json = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, json);
        var activacion = JsonSerializer.Deserialize<ActivarVersionImportacionDto>(json, JsonOpts)!;
        activacion.Resumen.Should().Be(new ResumenReconciliacionDto(1, 1, 1, 1));
        _output.WriteLine($"POST ACTIVAR CASO CONTROLADO status={(int)response.StatusCode}: {json}");

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var despues = await ConsultarMaestroAsync(db, 900);
            _output.WriteLine($"SQL DESPUÉS: {JsonSerializer.Serialize(despues)}");
            despues.Should().HaveCount(4);
            despues.Single(x => x.CodPred == 2).RequiereRevision.Should().BeTrue();
            despues.Single(x => x.CodPred == 2).DetalleRevision.Should()
                .Contain("Geometría inválida en importación versión 1:");
            despues.Single(x => x.CodPred == 3).Presente.Should().BeFalse();
            despues.Single(x => x.CodPred == 3).DetalleRevision.Should()
                .Contain("Ausente en dataset versión 1");
            var versionPersistida = await db.DatasetVersiones.AsNoTracking()
                .SingleAsync(x => x.Id == versionId);
            versionPersistida.ResumenReconciliacion.Should().Contain("altas");

            var generados = await db.Database.SqlQuery<DatosGeneradosSql>($"""
                SELECT
                  (SELECT COUNT(*)::int FROM dominio.historial_estados WHERE predio_id = {predioAusente.Id}) AS historial,
                  (SELECT COUNT(*)::int FROM dominio.documentos WHERE predio_id = {predioAusente.Id}) AS documentos
                """).SingleAsync();
            generados.Historial.Should().BeGreaterThan(0);
            generados.Documentos.Should().Be(1);
            _output.WriteLine($"SQL DATOS GENERADOS INTACTOS: historial={generados.Historial}, documentos={generados.Documentos}");

            var auditorias = await db.Auditorias
                .Where(x => x.EntidadTipo == nameof(Predio) &&
                            (x.EntidadId == predioSinCambio.Id.ToString() ||
                             x.EntidadId == predioActualizar.Id.ToString() ||
                             x.EntidadId == predioAusente.Id.ToString()))
                .ToListAsync();
            auditorias.Any(x =>
                x.EntidadId == predioActualizar.Id.ToString() &&
                x.ValorNuevo is not null &&
                x.ValorNuevo.Contains(versionId.ToString(), StringComparison.Ordinal))
                .Should().BeTrue();
            auditorias.Any(x =>
                x.EntidadId == predioSinCambio.Id.ToString() &&
                x.ValorNuevo is not null &&
                x.ValorNuevo.Contains(versionId.ToString(), StringComparison.Ordinal))
                .Should().BeFalse();
        }
    }

    [Fact]
    public async Task Activar_TripleteDuplicado_RechazaConBloqueanteB1()
    {
        var municipio = $"TD-{Guid.NewGuid():N}"[..12];
        var versionId = await CrearVersionPreviewAsync(municipio, 1,
        [
            new ParcelaPrueba(910, 1, 1, 100m, 100, "Duplicado A", false),
            new ParcelaPrueba(910, 1, 1, 100m, 120, "Duplicado B", false),
        ]);

        var response = await _clientAdmin.PostAsync(
            $"/api/importaciones/versiones/{versionId}/activar",
            null);
        var error = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        error.Should().Contain("B1");
        _output.WriteLine($"BLOQUEANTE B1 status={(int)response.StatusCode}: {error}");
    }

    [Fact]
    public async Task Activar_SuperficieNoPositiva_RechazaConBloqueanteB4()
    {
        var municipio = $"TS-{Guid.NewGuid():N}"[..12];
        var versionId = await CrearVersionPreviewAsync(municipio, 1,
        [
            new ParcelaPrueba(920, 1, 1, 0m, 100, "Superficie cero", false),
        ]);
        var bloqueante = (await ObtenerEstadoAsync(versionId)).ReportePreliminar.Validacion!
            .Bloqueantes.Single(x => x.Codigo == "B4");
        bloqueante.Conteo.Should().Be(1);
        bloqueante.Ejemplos.Should().Contain("1");

        var response = await _clientAdmin.PostAsync(
            $"/api/importaciones/versiones/{versionId}/activar",
            null);
        var error = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        error.Should().Contain("B4");
        _output.WriteLine($"BLOQUEANTE B4 status={(int)response.StatusCode}: {error}");
    }

    [Fact]
    public async Task Reactivar_Archivada_UsaMismoCaminoYQuedaComoUnicaActiva()
    {
        await LimpiarMaestroAsync();
        var municipio = $"TR-{Guid.NewGuid():N}"[..12];
        var version1 = await CrearVersionPreviewAsync(municipio, 1,
        [
            new ParcelaPrueba(930, 1, 1, 100m, 100, "Versión uno", false),
        ]);
        (await _clientAdmin.PostAsync($"/api/importaciones/versiones/{version1}/activar", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var version2 = await CrearVersionPreviewAsync(municipio, 2,
        [
            new ParcelaPrueba(930, 1, 1, 120m, 100, "Versión dos", false),
        ]);
        (await ObtenerEstadoAsync(version2)).ReportePreliminar.Validacion!
            .DiferenciasContraActiva.Should().HaveCount(7);
        (await _clientAdmin.PostAsync($"/api/importaciones/versiones/{version2}/activar", null))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var reactivar = await _clientAdmin.PostAsync(
            $"/api/importaciones/versiones/{version1}/activar",
            null);
        var json = await reactivar.Content.ReadAsStringAsync();

        reactivar.StatusCode.Should().Be(HttpStatusCode.OK, json);
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        (await db.DatasetVersiones.CountAsync(x =>
            x.MunicipioCodigo == municipio && x.Estado == EstadoDatasetVersion.Activa))
            .Should().Be(1);
        (await db.DatasetVersiones.SingleAsync(x => x.Id == version1)).Estado
            .Should().Be(EstadoDatasetVersion.Activa);
        (await db.DatasetVersiones.SingleAsync(x => x.Id == version2)).Estado
            .Should().Be(EstadoDatasetVersion.Archivada);
        _output.WriteLine($"REACTIVACIÓN status={(int)reactivar.StatusCode}: {json}");
    }

    [Fact]
    public async Task ConfirmarLegado_Retorna410Gone()
    {
        var response = await _clientAdmin.PostAsync(
            $"/api/importaciones/{Guid.NewGuid()}/confirmar",
            null);
        var error = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Gone);
        error.Should().Contain("flujo fue retirado");
        _output.WriteLine($"LEGADO CONFIRMAR status={(int)response.StatusCode}: {error}");
    }

    private async Task<Guid> CrearVersionPreviewAsync(
        string municipio,
        int numeroVersion,
        IReadOnlyList<ParcelaPrueba> parcelas)
    {
        await using var dbCarga = CrearContextoSinInterceptors();
        var version = DatasetVersion.Crear(numeroVersion, municipio, null, "Fixture activación");
        dbCarga.DatasetVersiones.Add(version);
        await dbCarga.SaveChangesAsync();

        var fila = 1;
        foreach (var parcela in parcelas)
        {
            dbCarga.CapasParcelas.Add(CapaParcela.Crear(
                version.Id,
                CrearPoligono(parcela.Coordenada, parcela.Invalida),
                parcela.CodUv,
                parcela.CodMan,
                parcela.CodPred,
                "{}",
                fila++,
                $"{parcela.CodUv}{parcela.CodMan}{parcela.CodPred}",
                parcela.Superficie,
                null,
                "R",
                null,
                null,
                null,
                null,
                parcela.Propietario,
                "Vía",
                "Barrio",
                "Dirección",
                null,
                null));
        }

        AgregarCapasAuxiliares(dbCarga, version.Id);
        await dbCarga.SaveChangesAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reporteServicio = scope.ServiceProvider.GetRequiredService<IReportePreviewVersionServicio>();
        var reporte = await reporteServicio.GenerarAsync(version.Id);
        var versionPersistida = await db.DatasetVersiones.SingleAsync(x => x.Id == version.Id);
        versionPersistida.RegistrarReportePreview(JsonSerializer.Serialize(reporte, JsonOpts));
        versionPersistida.MarcarPreviewListo();
        await db.SaveChangesAsync();
        _output.WriteLine($"REPORTE {municipio}: {JsonSerializer.Serialize(reporte, JsonOpts)}");
        return version.Id;
    }

    private async Task<EstadoVersionImportacionDto> ObtenerEstadoAsync(Guid versionId)
    {
        var response = await _clientAdmin.GetAsync($"/api/importaciones/versiones/{versionId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EstadoVersionImportacionDto>(JsonOpts))!;
    }

    private ApplicationDbContext CrearContextoSinInterceptors()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString, npgsql => npgsql.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void AgregarCapasAuxiliares(ApplicationDbContext db, Guid versionId)
    {
        var poligono = CrearPoligono(1000, false);
        db.CapasEdificaciones.Add(CapaEdificacion.Crear(
            versionId, poligono, "{}", 1, 1, "E", 1, 1, 1, 1, 1, null, null, 10));
        db.CapasPrediosNoFotografiados.Add(CapaPredioNoFotografiado.Crear(
            versionId, poligono, "{}", 1, 1, "P", 1, 1, 1, null, null, null, null));
        db.CapasManzanas.Add(CapaManzana.Crear(versionId, poligono, "{}", 1, "M", 1, 1, null));
        db.CapasDistritos.Add(CapaDistrito.Crear(versionId, poligono, "{}", 1, "D", 1, "Distrito"));
        db.CapasZonas.Add(CapaZona.Crear(versionId, poligono, "{}", 1, "Zona", 1, "Z"));
        db.CapasVias.Add(CapaVia.Crear(
            versionId,
            new LineString([new Coordinate(0, 0), new Coordinate(10, 10)]) { SRID = 32719 },
            "{}", 1, "Asfalto", "Vía", "Local", 10));
    }

    private static Predio CrearPredioMaestro(
        int codUv,
        int codMan,
        int codPred,
        decimal superficie,
        double coordenada,
        string propietario,
        Guid usuarioId)
    {
        var ubicacion = UbicacionCatastral.Crear(
            codUv.ToString(CultureInfo.InvariantCulture),
            codMan.ToString(CultureInfo.InvariantCulture),
            codPred.ToString(CultureInfo.InvariantCulture),
            "Barrio",
            "Dirección").Value;
        var predio = Predio.CrearImportado(
            ubicacion, superficie, usuarioId, propietario, "R", $"{codUv}{codMan}{codPred}").Value;
        predio.AsignarGeometria(
            GeometriaPredial.Crear(CrearPoligono(coordenada, false)).Value,
            usuarioId);
        predio.ActualizarSuperficieSig(100m);
        return predio;
    }

    private static Polygon CrearPoligono(double origen, bool invalida)
    {
        var factory = new GeometryFactory(new PrecisionModel(), 32719);
        var coordenadas = invalida
            ? new[]
            {
                new Coordinate(origen, origen), new Coordinate(origen + 10, origen + 10),
                new Coordinate(origen + 10, origen), new Coordinate(origen, origen + 10),
                new Coordinate(origen, origen),
            }
            : new[]
            {
                new Coordinate(origen, origen), new Coordinate(origen + 10, origen),
                new Coordinate(origen + 10, origen + 10), new Coordinate(origen, origen + 10),
                new Coordinate(origen, origen),
            };
        return factory.CreatePolygon(factory.CreateLinearRing(coordenadas));
    }

    private async Task LimpiarMaestroAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlRawAsync("DELETE FROM dominio.predios");
    }

    private static async Task<List<EstadoPredioSql>> ConsultarMaestroAsync(
        ApplicationDbContext db,
        int codUv) => await db.Database.SqlQuery<EstadoPredioSql>($"""
        SELECT cod_pred AS cod_pred,
               presente_en_version_activa AS presente,
               requiere_revision AS requiere_revision,
               detalle_revision AS detalle_revision
        FROM dominio.predios
        WHERE cod_uv = {codUv}
        ORDER BY cod_pred
        """).ToListAsync();

    private async Task<Guid> ObtenerAdminIdAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users
            .Where(x => x.Email == PostgreSqlFixture.AdminEmail)
            .Select(x => x.Id)
            .SingleAsync();
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

    private sealed record ParcelaPrueba(
        int CodUv,
        int CodMan,
        int CodPred,
        decimal? Superficie,
        double Coordenada,
        string Propietario,
        bool Invalida);

    private sealed class EstadoPredioSql
    {
        public int CodPred { get; init; }
        public bool Presente { get; init; }
        public bool RequiereRevision { get; init; }
        public string? DetalleRevision { get; init; }
    }

    private sealed class DatosGeneradosSql
    {
        public int Historial { get; init; }
        public int Documentos { get; init; }
    }
}
