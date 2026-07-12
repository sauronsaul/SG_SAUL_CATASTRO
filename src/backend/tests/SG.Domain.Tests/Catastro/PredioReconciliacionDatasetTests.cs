using FluentAssertions;
using NetTopologySuite.Geometries;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;
using SG.Domain.Catastro.ValueObjects;

namespace SG.Domain.Tests.Catastro;

public sealed class PredioReconciliacionDatasetTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    [Fact]
    public void CrearDesdeDataset_GeometriaInvalida_EntraMarcadoParaRevision()
    {
        var geometria = GeometriaPredial.CrearDesdeImportacion(CrearPoligonoInvalido()).Value;
        const string detalle = "Geometría inválida en importación versión 1: Self-intersection";

        var resultado = Predio.CrearDesdeDataset(
            CrearUbicacion(), 100m, 0m, geometria, Guid.NewGuid(), UsuarioId,
            detalleGeometriaInvalida: detalle);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.RequiereRevision.Should().BeTrue();
        resultado.Value.DetalleRevision.Should().Contain(detalle);
        resultado.Value.Geometria!.Poligono.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ReconciliarDesdeDataset_SinCambios_NoEscribeCambio()
    {
        var version1 = Guid.NewGuid();
        var geometria = GeometriaPredial.CrearDesdeImportacion(CrearPoligonoValido()).Value;
        var predio = Predio.CrearDesdeDataset(
            CrearUbicacion(), 100m, 100m, geometria, version1, UsuarioId).Value;

        var resultado = predio.ReconciliarDesdeDataset(
            CrearUbicacion(), 100m, 100m, geometria, Guid.NewGuid(), null, null, null, null);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeFalse();
        predio.UltimaVersionVistaId.Should().Be(version1);
    }

    [Fact]
    public void ReconciliarDesdeDataset_Reaparicion_NoLimpiaRevision()
    {
        var geometria = GeometriaPredial.CrearDesdeImportacion(CrearPoligonoValido()).Value;
        var predio = Predio.CrearDesdeDataset(
            CrearUbicacion(), 100m, 100m, geometria, Guid.NewGuid(), UsuarioId).Value;
        predio.MarcarAusenteEnDataset(2);

        var resultado = predio.ReconciliarDesdeDataset(
            CrearUbicacion(), 100m, 100m, geometria, Guid.NewGuid(), null, null, null, null);

        resultado.Value.Should().BeTrue();
        predio.PresenteEnVersionActiva.Should().BeTrue();
        predio.RequiereRevision.Should().BeTrue();
        predio.DetalleRevision.Should().Contain("Ausente en dataset versión 2");
    }

    [Fact]
    public void ReconciliarDesdeDataset_GeometriaInvalida_EstableceRevisionSinLimpiarAnterior()
    {
        var valida = GeometriaPredial.CrearDesdeImportacion(CrearPoligonoValido()).Value;
        var predio = Predio.CrearDesdeDataset(
            CrearUbicacion(), 100m, 100m, valida, Guid.NewGuid(), UsuarioId).Value;
        predio.MarcarAusenteEnDataset(2);
        var invalida = GeometriaPredial.CrearDesdeImportacion(CrearPoligonoInvalido()).Value;
        const string detalle = "Geometría inválida en importación versión 3: Self-intersection";

        var resultado = predio.ReconciliarDesdeDataset(
            CrearUbicacion(), 100m, 0m, invalida, Guid.NewGuid(), null, null, null, detalle);

        resultado.Value.Should().BeTrue();
        predio.RequiereRevision.Should().BeTrue();
        predio.DetalleRevision.Should().Contain("Ausente en dataset versión 2");
        predio.DetalleRevision.Should().Contain(detalle);
    }

    [Fact]
    public void ReconciliarDesdeDataset_PredioValidado_NoModificaEstadoNiCodigoOficial()
    {
        var predio = Predio.Crear(CrearUbicacion(), 100m, Guid.NewGuid(), UsuarioId).Value;
        predio.EnviarARevision(UsuarioId);
        var codigo = CodigoCatastral.Crear("02-006-028-001-0002-0003").Value;
        predio.Validar(codigo, UsuarioId);
        var geometria = GeometriaPredial.CrearDesdeImportacion(CrearPoligonoValido()).Value;

        var resultado = predio.ReconciliarDesdeDataset(
            CrearUbicacion(), 120m, 100m, geometria, Guid.NewGuid(),
            "Importado", "R", "ORIGEN", null);

        resultado.Value.Should().BeTrue();
        predio.Estado.Should().Be(EstadoPredio.Validado);
        predio.CodigoCatastral.Should().Be(codigo);
    }

    private static UbicacionCatastral CrearUbicacion() =>
        UbicacionCatastral.Crear("1", "2", "3", "Barrio", "Dirección").Value;

    private static Polygon CrearPoligonoValido()
    {
        var factory = new GeometryFactory(new PrecisionModel(), GeometriaPredial.SridObligatorio);
        return factory.CreatePolygon(factory.CreateLinearRing(
        [
            new Coordinate(0, 0), new Coordinate(10, 0), new Coordinate(10, 10),
            new Coordinate(0, 10), new Coordinate(0, 0),
        ]));
    }

    private static Polygon CrearPoligonoInvalido()
    {
        var factory = new GeometryFactory(new PrecisionModel(), GeometriaPredial.SridObligatorio);
        return factory.CreatePolygon(factory.CreateLinearRing(
        [
            new Coordinate(0, 0), new Coordinate(10, 10), new Coordinate(10, 0),
            new Coordinate(0, 10), new Coordinate(0, 0),
        ]));
    }
}
