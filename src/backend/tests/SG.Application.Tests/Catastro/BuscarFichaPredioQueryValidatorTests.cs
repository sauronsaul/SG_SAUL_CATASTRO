using FluentAssertions;
using SG.Application.Catastro.Predios.BuscarPorTriplete;

namespace SG.Application.Tests.Catastro;

public sealed class BuscarFichaPredioQueryValidatorTests
{
    private readonly BuscarFichaPredioQueryValidator _validator = new();

    [Theory]
    [InlineData(0, 1, 1, "Distrito")]
    [InlineData(1, 0, 1, "Manzana")]
    [InlineData(1, 1, 0, "Predio")]
    public void Validar_ComponenteMenorAUno_EsInvalido(
        int distrito,
        int manzana,
        int predio,
        string propiedad)
    {
        var resultado = _validator.Validate(
            new BuscarFichaPredioQuery("051201", distrito, manzana, predio));

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(x => x.PropertyName == propiedad);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(7, 200, 150)]
    public void Validar_ComponentesPositivos_EsValido(
        int distrito,
        int manzana,
        int predio)
    {
        var resultado = _validator.Validate(
            new BuscarFichaPredioQuery("051201", distrito, manzana, predio));

        resultado.IsValid.Should().BeTrue();
    }
}
