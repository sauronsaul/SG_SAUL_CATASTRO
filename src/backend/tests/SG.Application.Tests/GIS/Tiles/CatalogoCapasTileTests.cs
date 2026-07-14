using FluentAssertions;
using SG.Application.GIS.Tiles;

namespace SG.Application.Tests.GIS.Tiles;

public sealed class CatalogoCapasTileTests
{
    [Theory]
    [InlineData("parcelas", CapaTile.Parcelas)]
    [InlineData("edificaciones", CapaTile.Edificaciones)]
    [InlineData("predios-no-fotografiados", CapaTile.PrediosNoFotografiados)]
    [InlineData("manzanas", CapaTile.Manzanas)]
    [InlineData("distritos", CapaTile.Distritos)]
    [InlineData("zonas", CapaTile.Zonas)]
    [InlineData("vias", CapaTile.Vias)]
    public void IntentarResolver_CapaPermitida_ResuelveNombreExacto(string nombre, CapaTile esperada)
    {
        var resultado = CatalogoCapasTile.IntentarResolver(nombre, out var capa);

        resultado.Should().BeTrue();
        capa.Should().Be(esperada);
    }

    [Theory]
    [InlineData("parcelas;DROP TABLE dominio.capa_parcelas")]
    [InlineData("capa_parcelas")]
    [InlineData("Parcelas")]
    public void IntentarResolver_CapaNoPermitida_LaRechaza(string nombre)
    {
        CatalogoCapasTile.IntentarResolver(nombre, out _).Should().BeFalse();
    }
}
