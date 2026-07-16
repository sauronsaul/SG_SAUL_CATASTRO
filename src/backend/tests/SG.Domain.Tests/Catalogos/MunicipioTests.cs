using FluentAssertions;
using SG.Domain.Catalogos;

namespace SG.Domain.Tests.Catalogos;

public sealed class MunicipioTests
{
    [Fact]
    public void Crear_CodigoIneValido_NormalizaDatos()
    {
        var resultado = Municipio.Crear(
            "051201",
            " Uyuni ",
            " GOBIERNO AUTONOMO MUNICIPAL DE UYUNI ",
            " Potosi ",
            " INE Bolivia ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.CodigoIne.Should().Be("051201");
        resultado.Value.Nombre.Should().Be("Uyuni");
    }

    [Theory]
    [InlineData("UYUNI")]
    [InlineData("05120")]
    [InlineData("05120A")]
    [InlineData("１２３４５６")]
    public void Crear_CodigoIneInvalido_RetornaFailure(string codigo)
    {
        var resultado = Municipio.Crear(codigo, "Nombre", "Nombre oficial", "Departamento", "Fuente");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(MunicipioErrores.CodigoIneInvalido);
    }
}
