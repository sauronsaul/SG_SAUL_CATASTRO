using FluentAssertions;
using NSubstitute;
using SG.Application.Abstractions.Autenticacion;
using SG.Application.Autenticacion.Logout;

namespace SG.Application.Tests.Autenticacion;

public sealed class LogoutCommandHandlerTests
{
    private readonly IRefreshTokenRepositorio _refreshTokens = Substitute.For<IRefreshTokenRepositorio>();
    private readonly IAuditoriaService _auditoria = Substitute.For<IAuditoriaService>();

    private LogoutCommandHandler CrearHandler() => new(_refreshTokens, _auditoria);

    [Fact]
    public async Task Handle_TokenActivo_RevocaToken()
    {
        var tokenId = Guid.NewGuid();
        var tokenDto = new RefreshTokenActivoDto(tokenId, Guid.NewGuid(), IsActive: true, EstaRevocado: false, EstaExpirado: false, ReplacedByToken: null);
        _refreshTokens.BuscarPorTokenAsync("token-valido", Arg.Any<CancellationToken>()).Returns(tokenDto);
        var command = new LogoutCommand("token-valido", "127.0.0.1");

        await CrearHandler().Handle(command, CancellationToken.None);

        await _refreshTokens.Received(1).RevocarAsync(
            tokenId, "127.0.0.1", null, "logout", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TokenActivo_AuditaConResultadoOKSinMotivo()
    {
        var tokenId = Guid.NewGuid();
        var usuarioId = Guid.NewGuid();
        var tokenDto = new RefreshTokenActivoDto(tokenId, usuarioId, IsActive: true, EstaRevocado: false, EstaExpirado: false, ReplacedByToken: null);
        _refreshTokens.BuscarPorTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(tokenDto);
        var command = new LogoutCommand("token-valido", "127.0.0.1");

        await CrearHandler().Handle(command, CancellationToken.None);

        await _auditoria.Received(1).RegistrarAsync(
            "identidad", "logout", usuarioId, "RefreshToken", tokenId.ToString(),
            "OK", "127.0.0.1", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_TokenInexistente_NoRevocaYAuditaConMotivo()
    {
        _refreshTokens.BuscarPorTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((RefreshTokenActivoDto?)null);
        var command = new LogoutCommand("token-inexistente", "127.0.0.1");

        var result = await CrearHandler().Handle(command, CancellationToken.None);

        await _refreshTokens.DidNotReceive().RevocarAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _auditoria.Received(1).RegistrarAsync(
            "identidad", "logout", null, "RefreshToken", "desconocido",
            "OK", "127.0.0.1", "token_inexistente", Arg.Any<CancellationToken>());
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_TokenRevocado_NoRevocaYAuditaConMotivo()
    {
        var tokenDto = new RefreshTokenActivoDto(Guid.NewGuid(), Guid.NewGuid(), IsActive: false, EstaRevocado: true, EstaExpirado: false, ReplacedByToken: null);
        _refreshTokens.BuscarPorTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(tokenDto);
        var command = new LogoutCommand("token-revocado", "127.0.0.1");

        var result = await CrearHandler().Handle(command, CancellationToken.None);

        await _refreshTokens.DidNotReceive().RevocarAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
        await _auditoria.Received(1).RegistrarAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string?>(), "token_inexistente", Arg.Any<CancellationToken>());
        result.IsSuccess.Should().BeTrue();
    }
}
