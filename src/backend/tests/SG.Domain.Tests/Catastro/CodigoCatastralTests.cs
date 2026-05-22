using FluentAssertions;
using SG.Domain.Catastro.ValueObjects;

namespace SG.Domain.Tests.Catastro;

public sealed class CodigoCatastralTests
{
    [Fact]
    public void Crear_ConGuionesValidos_EsExito_ValorCanonicoCorrector()
    {
        var result = CodigoCatastral.Crear("02-006-028-001-0001-0001");

        result.IsSuccess.Should().BeTrue();
        result.Value.Departamento.Should().Be("02");
        result.Value.Provincia.Should().Be("006");
        result.Value.Municipio.Should().Be("028");
        result.Value.Zona.Should().Be("001");
        result.Value.Manzana.Should().Be("0001");
        result.Value.Lote.Should().Be("0001");
        result.Value.Valor.Should().Be("02-006-028-001-0001-0001");
    }

    [Fact]
    public void Crear_SinGuiones_EsExitoEquivalente()
    {
        // 02 006 028 001 0001 0001 concatenado = 19 chars
        var result = CodigoCatastral.Crear("0200602800100010001");

        result.IsSuccess.Should().BeTrue();
        result.Value.Valor.Should().Be("02-006-028-001-0001-0001");
    }

    [Fact]
    public void Crear_MismoValorConYSinGuiones_SonIguales()
    {
        var conGuiones = CodigoCatastral.Crear("02-006-028-001-0001-0001").Value;
        var sinGuiones = CodigoCatastral.Crear("0200602800100010001").Value;

        conGuiones.Should().Be(sinGuiones);
    }

    [Fact]
    public void Crear_SegmentoConLetras_EsFailure()
    {
        var result = CodigoCatastral.Crear("02-006-028-AAA-0001-0001");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CodigoCatastralErrores.FormatoInvalido);
    }

    [Fact]
    public void Crear_EntradaVacia_EsFailure()
    {
        var result = CodigoCatastral.Crear(string.Empty);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CodigoCatastralErrores.EntradaVacia);
    }

    [Fact]
    public void Crear_SoloEspacios_EsFailure()
    {
        var result = CodigoCatastral.Crear("   ");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CodigoCatastralErrores.EntradaVacia);
    }

    [Fact]
    public void Crear_NumeroIncorrectoDeParts_EsFailure()
    {
        // Solo 5 segmentos en vez de 6
        var result = CodigoCatastral.Crear("02-006-028-001-0001");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CodigoCatastralErrores.FormatoInvalido);
    }

    [Fact]
    public void Crear_SegmentoLongitudIncorrecta_EsFailure()
    {
        // Zona debe ser 3 dígitos, no 2
        var result = CodigoCatastral.Crear("02-006-028-01-0001-0001");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(CodigoCatastralErrores.FormatoInvalido);
    }

    [Fact]
    public void ToString_RetornaValorCanonico()
    {
        var codigo = CodigoCatastral.Crear("02-006-028-001-0001-0001").Value;

        codigo.ToString().Should().Be("02-006-028-001-0001-0001");
    }

    [Fact]
    public void Crear_ZonaConPaddingCeros_SegmentoAlmacenadoCorrectamente()
    {
        // Verifica que zona "001" (con padding) se almacena tal cual
        var result = CodigoCatastral.Crear("02-006-028-001-0001-0001");

        result.Value.Zona.Should().Be("001");
        result.Value.Manzana.Should().Be("0001");
        result.Value.Lote.Should().Be("0001");
    }
}
