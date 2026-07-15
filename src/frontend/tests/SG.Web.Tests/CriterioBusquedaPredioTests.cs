using FluentAssertions;
using SG.Web.Models;

namespace SG.Web.Tests;

public sealed class CriterioBusquedaPredioTests
{
    [Theory]
    [InlineData(null, 1, 1)]
    [InlineData(0, 1, 1)]
    [InlineData(1, null, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, null)]
    [InlineData(1, 1, 0)]
    public void Crear_ConComponenteAusenteONoPositivo_Rechaza(
        int? distrito,
        int? manzana,
        int? predio)
    {
        var resultado = CriterioBusquedaPredio.Crear(distrito, manzana, predio);

        resultado.EsValido.Should().BeFalse();
        resultado.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Crear_ConDistritoMayorASeis_AceptaContratoGenerico()
    {
        var resultado = CriterioBusquedaPredio.Crear(7, 20, 30);

        resultado.EsValido.Should().BeTrue();
        resultado.Criterio.Should().Be(new CriterioBusquedaPredio(7, 20, 30));
    }
}
