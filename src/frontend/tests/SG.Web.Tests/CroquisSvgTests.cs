using FluentAssertions;
using SG.Contracts.Catastro;
using SG.Web.Models;

namespace SG.Web.Tests;

public sealed class CroquisSvgTests
{
    [Fact]
    public void Crear_ConservaMetricaOrientaNorteYDimensionaBarraGrafica()
    {
        var geometria = CrearGeometria(
            [[
                [500000, 7700000],
                [500100, 7700000],
                [500100, 7700050],
                [500000, 7700050],
                [500000, 7700000]
            ]]);

        var resultado = CroquisSvg.Crear(geometria);

        resultado.Trayectoria.Should().StartWith("M 55 447.5 L 845 447.5 L 845 52.5");
        resultado.Trayectoria.Should().EndWith("Z");
        resultado.BarraMetros.Should().BeGreaterThan(0);
        ((resultado.BarraX2 - resultado.BarraX1) * resultado.MetrosPorUnidadSvg)
            .Should().BeApproximately(resultado.BarraMetros, 0.000001);
        resultado.EscalaNominal.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Crear_PreservaAnilloInteriorEnTrayectoriaEvenOdd()
    {
        var geometria = CrearGeometria(
            [
                [500000, 7700000], [500100, 7700000], [500100, 7700100],
                [500000, 7700100], [500000, 7700000]
            ],
            [
                [500020, 7700020], [500040, 7700020], [500040, 7700040],
                [500020, 7700040], [500020, 7700020]
            ]);

        var resultado = CroquisSvg.Crear(geometria);

        resultado.Trayectoria.Count(caracter => caracter == 'M').Should().Be(2);
        resultado.Trayectoria.Count(caracter => caracter == 'Z').Should().Be(2);
    }

    [Fact]
    public void Crear_RechazaSridQueNoSeaUtm19Sur()
    {
        var geometria = CrearGeometria(
            [[500000, 7700000], [500010, 7700000], [500010, 7700010], [500000, 7700000]])
            with { Srid = 4326 };

        var accion = () => CroquisSvg.Crear(geometria);

        accion.Should().Throw<ArgumentException>()
            .WithMessage("*EPSG:32719*");
    }

    [Fact]
    public void FechaEmisionBolivia_ConvierteUtcAOffsetMenosCuatro()
    {
        var utc = new DateTimeOffset(2026, 7, 15, 4, 30, 0, TimeSpan.Zero);

        var bolivia = FechaEmisionBolivia.DesdeUtc(utc);

        bolivia.Offset.Should().Be(TimeSpan.FromHours(-4));
        bolivia.Date.Should().Be(new DateTime(2026, 7, 15));
        bolivia.Hour.Should().Be(0);
        bolivia.Minute.Should().Be(30);
    }

    private static GeometriaPlanarDto CrearGeometria(params double[][][] anillos) =>
        new(32719, "Polygon", anillos);
}
