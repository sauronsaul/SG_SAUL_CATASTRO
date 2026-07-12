using FluentAssertions;
using NSubstitute;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Importacion;
using SG.Application.Importacion.Versiones;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Tests.Importacion;

public sealed class ActivarVersionImportacionHandlerTests
{
    private readonly IActivacionVersionServicio _activacion = Substitute.For<IActivacionVersionServicio>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    [Fact]
    public async Task Handle_SinUsuario_RetornaErrorSinInvocarServicio()
    {
        _currentUser.UserId.Returns((Guid?)null);
        var handler = new ActivarVersionImportacionHandler(_activacion, _currentUser);

        var resultado = await handler.Handle(
            new ActivarVersionImportacionCommand(Guid.NewGuid()),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(VersionImportacionErrores.UsuarioNoDisponible);
        await _activacion.DidNotReceiveWithAnyArgs()
            .ActivarAsync(default, default, default);
    }

    [Fact]
    public async Task Handle_UsuarioIdentificado_DelegaActivacion()
    {
        var usuarioId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var dto = new ActivarVersionImportacionDto(
            versionId,
            "Activa",
            new ResumenReconciliacionDto(1, 2, 3, 4));
        _currentUser.UserId.Returns(usuarioId);
        _activacion.ActivarAsync(versionId, usuarioId, Arg.Any<CancellationToken>())
            .Returns(Result.Success(dto));
        var handler = new ActivarVersionImportacionHandler(_activacion, _currentUser);

        var resultado = await handler.Handle(
            new ActivarVersionImportacionCommand(versionId),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().Be(dto);
        await _activacion.Received(1)
            .ActivarAsync(versionId, usuarioId, Arg.Any<CancellationToken>());
    }
}
