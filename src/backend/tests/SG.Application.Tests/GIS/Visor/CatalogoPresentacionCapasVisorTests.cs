using FluentAssertions;
using SG.Application.GIS.Visor;
using SG.Domain.Importacion;

namespace SG.Application.Tests.GIS.Visor;

public sealed class CatalogoPresentacionCapasVisorTests
{
    [Theory]
    [InlineData("parcelas", TipoCapa.Predios)]
    [InlineData("edificaciones", TipoCapa.Construcciones)]
    [InlineData("predios-no-fotografiados", TipoCapa.PrediosNoFotografiados)]
    [InlineData("manzanas", TipoCapa.Manzanas)]
    [InlineData("distritos", TipoCapa.Distritos)]
    [InlineData("zonas", TipoCapa.ZonasValuacion)]
    [InlineData("vias", TipoCapa.Vias)]
    [InlineData("areas-urbanas", TipoCapa.AreasUrbanas)]
    [InlineData("puntos-geodesicos", TipoCapa.PuntosGeodesicos)]
    public void IntentarResolver_CapaPermitida_ResuelveTipo(string nombre, TipoCapa esperado)
    {
        CatalogoPresentacionCapasVisor.IntentarResolver(nombre, out var tipo).Should().BeTrue();
        tipo.Should().Be(esperado);
    }

    [Theory]
    [InlineData("parcelas;DROP TABLE dominio.capa_parcelas")]
    [InlineData("capa_parcelas")]
    [InlineData("Parcelas")]
    public void IntentarResolver_CapaNoPermitida_LaRechaza(string nombre) =>
        CatalogoPresentacionCapasVisor.IntentarResolver(nombre, out _).Should().BeFalse();
}
