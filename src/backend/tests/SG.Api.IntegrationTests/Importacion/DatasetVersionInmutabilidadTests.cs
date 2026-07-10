using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SG.Api.IntegrationTests.Infrastructure;
using ImportacionDomain = SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;
using Xunit.Abstractions;

namespace SG.Api.IntegrationTests.Importacion;

[Collection("Postgres")]
public sealed class DatasetVersionInmutabilidadTests : IDisposable
{
    private readonly SgApiFactory _factory;
    private readonly ITestOutputHelper _output;

    public DatasetVersionInmutabilidadTests(PostgreSqlFixture fixture, ITestOutputHelper output)
    {
        _factory = new SgApiFactory(fixture.ConnectionString);
        _output = output;
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Trigger_UpdateEnVersionActiva_RechazaConEstado()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var version = await CrearVersionAsync(db, ImportacionDomain.EstadoDatasetVersion.Activa);
        var capaId = await InsertarParcelaAsync(db, version.Id);

        var act = async () => await db.Database.ExecuteSqlRawAsync(
            "UPDATE dominio.capa_parcelas SET fila_origen = 2 WHERE id = {0}", capaId);

        var exception = await act.Should().ThrowAsync<Exception>();
        _output.WriteLine(exception.Which.Message);
        exception.WithMessage("*capa_parcelas*UPDATE*Activa*");
    }

    [Fact]
    public async Task Trigger_UpdateEnVersionEnCarga_PermiteModificar()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var version = await CrearVersionAsync(db, ImportacionDomain.EstadoDatasetVersion.EnCarga);
        var capaId = await InsertarParcelaAsync(db, version.Id);

        var filas = await db.Database.ExecuteSqlRawAsync(
            "UPDATE dominio.capa_parcelas SET fila_origen = 2 WHERE id = {0}", capaId);

        filas.Should().Be(1);
    }

    [Theory]
    [InlineData(ImportacionDomain.EstadoDatasetVersion.Fallida)]
    [InlineData(ImportacionDomain.EstadoDatasetVersion.Descartada)]
    public async Task Trigger_DeleteEnVersionPurgable_PermiteEliminar(
        ImportacionDomain.EstadoDatasetVersion estado)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var version = await CrearVersionAsync(db, estado);
        var capaId = await InsertarParcelaAsync(db, version.Id);

        var filas = await db.Database.ExecuteSqlRawAsync(
            "DELETE FROM dominio.capa_parcelas WHERE id = {0}", capaId);

        filas.Should().Be(1);
    }

    [Fact]
    public async Task Trigger_TruncateSiempre_Rechaza()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var act = async () => await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE dominio.capa_parcelas;");

        await act.Should().ThrowAsync<Exception>()
            .WithMessage("*capa_parcelas*TRUNCATE*prohibida*");
    }

    [Fact]
    public async Task Esquema_ContieneOchoTablasYDosIndicesUnicosParciales()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var tablas = await db.Database.SqlQueryRaw<string>(@"
SELECT table_name AS ""Value""
FROM information_schema.tables
WHERE table_schema = 'dominio'
  AND table_name IN (
    'dataset_versiones', 'capa_parcelas', 'capa_edificaciones',
    'capa_predios_no_fotografiados', 'capa_manzanas', 'capa_distritos',
    'capa_zonas', 'capa_vias')
ORDER BY table_name")
            .ToListAsync();

        var indicesParciales = await db.Database.SqlQueryRaw<string>(@"
SELECT indexname || ' | ' || indexdef AS ""Value""
FROM pg_indexes
WHERE schemaname = 'dominio'
  AND indexname IN (
    'uix_dataset_versiones_municipio_activa',
    'uix_predios_triplete_activo')
  AND indexdef LIKE '% WHERE %'
ORDER BY indexname")
            .ToListAsync();

        _output.WriteLine("TABLAS:");
        foreach (var tabla in tablas)
            _output.WriteLine(tabla);

        _output.WriteLine("INDICES:");
        foreach (var indice in indicesParciales)
            _output.WriteLine(indice);

        tablas.Should().HaveCount(8);
        indicesParciales.Should().HaveCount(2);
    }

    private static async Task<ImportacionDomain.DatasetVersion> CrearVersionAsync(
        ApplicationDbContext db,
        ImportacionDomain.EstadoDatasetVersion estado)
    {
        var version = ImportacionDomain.DatasetVersion.Crear(
            1,
            $"TST-{Guid.NewGuid():N}"[..12],
            null,
            "Versión de prueba");

        switch (estado)
        {
            case ImportacionDomain.EstadoDatasetVersion.Activa:
                version.MarcarPreviewListo();
                version.Activar(Guid.NewGuid());
                break;
            case ImportacionDomain.EstadoDatasetVersion.Fallida:
                version.MarcarFallida();
                break;
            case ImportacionDomain.EstadoDatasetVersion.Descartada:
                version.MarcarPreviewListo();
                version.Descartar();
                break;
        }

        db.DatasetVersiones.Add(version);
        await db.SaveChangesAsync();
        return version;
    }

    private static async Task<Guid> InsertarParcelaAsync(ApplicationDbContext db, Guid datasetVersionId)
    {
        var capaId = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync(@"
INSERT INTO dominio.capa_parcelas
    (id, dataset_version_id, geometria, cod_uv, cod_man, cod_pred, atributos_extra, fila_origen)
VALUES
    ({0}, {1}, ST_GeomFromText('POLYGON((500000 8000000,500100 8000000,500100 8000100,500000 8000100,500000 8000000))', 32719),
     1, 1, 1, '{{}}'::jsonb, 1)", capaId, datasetVersionId);
        return capaId;
    }
}
