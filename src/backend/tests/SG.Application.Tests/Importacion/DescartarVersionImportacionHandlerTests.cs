using FluentAssertions;
using NSubstitute;
using SG.Application.Abstractions.Importacion;
using SG.Application.Importacion.Versiones;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Tests.Importacion;

public sealed class DescartarVersionImportacionHandlerTests
{
    private readonly ICargaVersionadaServicio _carga =
        Substitute.For<ICargaVersionadaServicio>();

    [Fact]
    public async Task Handle_VersionDescartable_DelegaYRetornaDto()
    {
        var versionId = Guid.NewGuid();
        var dto = new DescartarVersionImportacionDto(versionId, "Descartada");
        _carga.DescartarYPurgarAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));
        var handler = new DescartarVersionImportacionHandler(_carga);

        var resultado = await handler.Handle(
            new DescartarVersionImportacionCommand(versionId),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(dto);
        await _carga.Received(1)
            .DescartarYPurgarAsync(versionId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_VersionInexistente_PropagaError()
    {
        var versionId = Guid.NewGuid();
        _carga.DescartarYPurgarAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure<DescartarVersionImportacionDto>(
                VersionImportacionErrores.NoEncontrada));
        var handler = new DescartarVersionImportacionHandler(_carga);

        var resultado = await handler.Handle(
            new DescartarVersionImportacionCommand(versionId),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(VersionImportacionErrores.NoEncontrada);
    }
}
