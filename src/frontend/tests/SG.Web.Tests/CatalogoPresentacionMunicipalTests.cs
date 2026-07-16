using FluentAssertions;
using SG.Web.Models;

namespace SG.Web.Tests;

public sealed class CatalogoPresentacionMunicipalTests
{
    [Fact]
    public void ContieneLosQuinceCodigosObservadosSinInventarEtiquetas()
    {
        CatalogoPresentacionMunicipal.CodigosConocidos.Should().BeEquivalentTo(
            [
                "CMC", "COM", "CUL", "DEP", "EDU", "IND", "OFI", "REC",
                "REL", "SAL", "SER", "SIN", "TRR", "TRU", "VIV"
            ]);

        var presentacion = CatalogoPresentacionMunicipal.Crear("SIN");

        presentacion.Texto.Should().Be("SIN — código de origen");
        presentacion.Ayuda.Should().Be(
            "Código del catálogo municipal pendiente de diccionario oficial.");
        presentacion.EsCodigoConocido.Should().BeTrue();
    }

    [Fact]
    public void CodigoNoCatalogado_ConservaValorCrudoYExponeAyuda()
    {
        var presentacion = CatalogoPresentacionMunicipal.Crear("ABC");

        presentacion.Texto.Should().Be("ABC");
        presentacion.Ayuda.Should().Contain("pendiente de diccionario oficial");
        presentacion.EsCodigoConocido.Should().BeFalse();
    }

    [Fact]
    public void ValorAusente_MuestraNoRegistradoSinTratarloComoCodigo()
    {
        var presentacion = CatalogoPresentacionMunicipal.Crear(null);

        presentacion.Texto.Should().Be("No registrado");
        presentacion.Ayuda.Should().BeNull();
        presentacion.EsCodigoConocido.Should().BeFalse();
    }
}
