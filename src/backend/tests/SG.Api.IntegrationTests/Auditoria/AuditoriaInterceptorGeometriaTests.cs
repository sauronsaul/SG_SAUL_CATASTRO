using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Geometries;
using SG.Api.IntegrationTests.Infrastructure;
using SG.Domain.Catastro;
using SG.Domain.Catastro.ValueObjects;
using SG.Infrastructure.Auditoria;
using SG.Infrastructure.Persistencia;

namespace SG.Api.IntegrationTests.Auditoria;

[Collection("Postgres")]
public sealed class AuditoriaInterceptorGeometriaTests : IDisposable
{
    private readonly SgApiFactory _factory;

    public AuditoriaInterceptorGeometriaTests(PostgreSqlFixture fixture)
    {
        _factory = new SgApiFactory(fixture.ConnectionString);
    }

    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// Verifica que el AuditoriaInterceptor NO serializa el Polygon de NTS en valor_nuevo.
    /// Sin la exclusión, cada predio con geometría generaría un blob GeoJSON en auditoría.
    /// </summary>
    [Fact]
    public async Task Predio_ConGeometria_AuditoriaValorNuevo_NoContienePoligono()
    {
        // Arrange
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var ubicacion = UbicacionCatastral.Crear("AUDIT", "GEO", "001").Value;
        var predio = Predio.CrearImportado(ubicacion, 150m, Guid.NewGuid()).Value;

        var geoFactory = new GeometryFactory(
            new PrecisionModel(), GeometriaPredial.SridObligatorio);
        var coords = new[]
        {
            new Coordinate(500000, 8000000),
            new Coordinate(500100, 8000000),
            new Coordinate(500100, 8000100),
            new Coordinate(500000, 8000100),
            new Coordinate(500000, 8000000),
        };
        var polygon = geoFactory.CreatePolygon(geoFactory.CreateLinearRing(coords));
        predio.AsignarGeometria(GeometriaPredial.Crear(polygon).Value, Guid.NewGuid());

        // Act
        db.Predios.Add(predio);
        await db.SaveChangesAsync();

        // Assert: el registro de auditoría del Predio no contiene datos de geometría
        var auditPredio = await db.Set<AuditoriaEntidad>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a =>
                a.EntidadTipo == nameof(Predio) &&
                a.EntidadId == predio.Id.ToString());

        auditPredio.Should().NotBeNull(
            "debe existir un registro de auditoría para el predio insertado");

        auditPredio!.ValorNuevo.Should().NotContain("Poligono",
            because: "el interceptor debe excluir la propiedad Poligono (NTS Polygon)");

        auditPredio.ValorNuevo.Should().NotContain("8000000",
            because: "las coordenadas UTM del polígono no deben aparecer en valor_nuevo");

        // Assert: no existe ningún registro de auditoría para el owned entity GeometriaPredial
        var auditGeo = await db.Set<AuditoriaEntidad>()
            .AsNoTracking()
            .AnyAsync(a => a.EntidadTipo == nameof(GeometriaPredial));

        auditGeo.Should().BeFalse(
            because: "GeometriaPredial es un owned entity de geometría y no debe auditarse");
    }
}
