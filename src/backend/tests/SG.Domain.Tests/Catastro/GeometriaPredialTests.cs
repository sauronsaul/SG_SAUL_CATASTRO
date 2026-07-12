using FluentAssertions;
using NetTopologySuite.Geometries;
using SG.Domain.Catastro.ValueObjects;

namespace SG.Domain.Tests.Catastro;

public sealed class GeometriaPredialTests
{
    private static Polygon PoligonoValido(int srid = GeometriaPredial.SridObligatorio)
    {
        var factory = new GeometryFactory(new PrecisionModel(), srid);
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 0),
            new Coordinate(10, 10),
            new Coordinate(0, 10),
            new Coordinate(0, 0),
        };
        return factory.CreatePolygon(factory.CreateLinearRing(coords));
    }

    [Fact]
    public void Crear_PoligonoValidoConSridCorrecto_EsExito()
    {
        var result = GeometriaPredial.Crear(PoligonoValido());

        result.IsSuccess.Should().BeTrue();
        result.Value.Poligono.SRID.Should().Be(GeometriaPredial.SridObligatorio);
        result.Value.CalcularAreaM2().Should().BeGreaterThan(0);
    }

    [Fact]
    public void Crear_PoligonoNulo_EsFailure()
    {
        var result = GeometriaPredial.Crear(null!);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GeometriaPredialErrores.PoligonoRequerido);
    }

    [Fact]
    public void Crear_SridIncorrecto_EsFailure()
    {
        var poligono = PoligonoValido(srid: 4326);

        var result = GeometriaPredial.Crear(poligono);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GeometriaPredialErrores.SridInvalido);
    }

    [Fact]
    public void Crear_PoligonoInvalido_EsFailure()
    {
        var factory = new GeometryFactory(new PrecisionModel(), GeometriaPredial.SridObligatorio);
        // Polígono auto-intersectante (mariposa) → IsValid = false
        var coords = new[]
        {
            new Coordinate(0, 0),
            new Coordinate(10, 10),
            new Coordinate(10, 0),
            new Coordinate(0, 10),
            new Coordinate(0, 0),
        };
        var invalid = factory.CreatePolygon(factory.CreateLinearRing(coords));

        var result = GeometriaPredial.Crear(invalid);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GeometriaPredialErrores.GeometriaInvalida);
    }

    [Fact]
    public void CrearDesdeImportacion_PoligonoTopologicamenteInvalido_EsExito()
    {
        var factory = new GeometryFactory(new PrecisionModel(), GeometriaPredial.SridObligatorio);
        var invalid = factory.CreatePolygon(factory.CreateLinearRing(
        [
            new Coordinate(0, 0), new Coordinate(10, 10), new Coordinate(10, 0),
            new Coordinate(0, 10), new Coordinate(0, 0),
        ]));

        var result = GeometriaPredial.CrearDesdeImportacion(invalid);

        result.IsSuccess.Should().BeTrue();
        result.Value.Poligono.IsValid.Should().BeFalse();
    }

    [Fact]
    public void CrearDesdeImportacion_SridIncorrecto_EsFailure()
    {
        var result = GeometriaPredial.CrearDesdeImportacion(PoligonoValido(4326));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GeometriaPredialErrores.SridInvalido);
    }

    [Fact]
    public void CalcularAreaM2_PoligonoCuadrado10x10_RetornaAreaCorrecta()
    {
        var geometria = GeometriaPredial.Crear(PoligonoValido()).Value;

        geometria.CalcularAreaM2().Should().BeApproximately(100.0, precision: 0.001);
    }
}
