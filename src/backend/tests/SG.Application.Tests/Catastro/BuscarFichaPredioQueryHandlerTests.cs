using FluentAssertions;
using NSubstitute;
using SG.Application.Abstractions.Catastro;
using SG.Application.Catastro.Predios.BuscarPorTriplete;
using SG.Contracts.Catastro;

namespace SG.Application.Tests.Catastro;

public sealed class BuscarFichaPredioQueryHandlerTests
{
    private readonly IConsultaPredioVersionado _consulta =
        Substitute.For<IConsultaPredioVersionado>();
    private readonly BuscarFichaPredioQueryValidator _validator = new();

    private BuscarFichaPredioQueryHandler CrearHandler() => new(_consulta, _validator);

    [Fact]
    public async Task Handle_CriterioInvalido_RetornaErrorSinConsultarPersistencia()
    {
        var resultado = await CrearHandler().Handle(
            new BuscarFichaPredioQuery(0, 1, 1),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(FichaPredioErrores.CriterioInvalido);
        await _consulta.DidNotReceive().BuscarAsync(
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TripleteAusente_RetornaNoEncontrado()
    {
        _consulta.BuscarAsync(7, 1, 1, Arg.Any<CancellationToken>())
            .Returns((FichaPredioDto?)null);

        var resultado = await CrearHandler().Handle(
            new BuscarFichaPredioQuery(7, 1, 1),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(FichaPredioErrores.NoEncontrado);
    }

    [Fact]
    public async Task Handle_TripleteExistente_RetornaFichaPersistida()
    {
        var ficha = CrearFicha();
        _consulta.BuscarAsync(1, 2, 3, Arg.Any<CancellationToken>())
            .Returns(ficha);

        var resultado = await CrearHandler().Handle(
            new BuscarFichaPredioQuery(1, 2, 3),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().BeSameAs(ficha);
    }

    private static FichaPredioDto CrearFicha() => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        3,
        "UYUNI",
        42,
        1,
        2,
        3,
        null,
        "01-02-03",
        "Importado",
        100m,
        99.5m,
        null,
        "Referencia",
        "VIV",
        "Via prueba",
        "Barrio prueba",
        null,
        "VIV",
        "PLA",
        "SI",
        "SI",
        "SI",
        "NO",
        new LimitesPredioDto(-66.9, -20.5, -66.8, -20.4));
}
