using FluentAssertions;
using NSubstitute;
using SG.Application.Abstractions.Autenticacion;
using SG.Application.Autenticacion;
using SG.Application.Autenticacion.Refresh;

namespace SG.Application.Tests.Autenticacion;

public sealed class RefreshTokenCommandHandlerTests
{
    private readonly IRefreshTokenRepositorio _refreshTokens = Substitute.For<IRefreshTokenRepositorio>();
    private readonly IUsuarioServicio _usuarios = Substitute.For<IUsuarioServicio>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IAuditoriaService _auditoria = Substitute.For<IAuditoriaService>();

    private RefreshTokenCommandHandler CrearHandler() =>
        new(_refreshTokens, _usuarios, _tokenService, _auditoria);

    [Fact]
    public async Task Handle_TokenNoExiste_RetornaTokenInvalido()
    {
        _refreshTokens.BuscarPorTokenAsync(Arg.Any<string>()).Returns((RefreshTokenActivoDto?)null);
        var command = new RefreshTokenCommand("token-inexistente", "127.0.0.1");

        var result = await CrearHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AutenticacionErrores.TokenInvalido);
    }

    [Fact]
    public async Task Handle_TokenRevocado_RevocaTodosYRetornaReutilizacion()
    {
        var usuarioId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var tokenDto = new RefreshTokenActivoDto(
            tokenId, usuarioId,
            IsActive: false, EstaRevocado: true, EstaExpirado: false, ReplacedByToken: "otro");
        _refreshTokens.BuscarPorTokenAsync(Arg.Any<string>()).Returns(tokenDto);
        var command = new RefreshTokenCommand("token-usado", "127.0.0.1");

        var result = await CrearHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AutenticacionErrores.ReutilizacionDetectada);
        await _refreshTokens.Received(1).RevocarTodosAsync(usuarioId, "127.0.0.1", "reutilizacion_detectada");
        await _auditoria.Received(1).RegistrarAsync(
            Arg.Any<string>(), "reutilizacion_detectada", usuarioId,
            Arg.Any<string>(), Arg.Any<string>(), "ERROR", Arg.Any<string?>());
    }

    [Fact]
    public async Task Handle_TokenExpirado_RetornaTokenExpirado()
    {
        var tokenDto = new RefreshTokenActivoDto(
            Guid.NewGuid(), Guid.NewGuid(),
            IsActive: false, EstaRevocado: false, EstaExpirado: true, ReplacedByToken: null);
        _refreshTokens.BuscarPorTokenAsync(Arg.Any<string>()).Returns(tokenDto);
        var command = new RefreshTokenCommand("token-expirado", "127.0.0.1");

        var result = await CrearHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AutenticacionErrores.TokenExpirado);
    }

    [Fact]
    public async Task Handle_TokenActivo_RotaTokensYRetornaRespuesta()
    {
        var usuarioId = Guid.NewGuid();
        var tokenId = Guid.NewGuid();
        var tokenDto = new RefreshTokenActivoDto(
            tokenId, usuarioId,
            IsActive: true, EstaRevocado: false, EstaExpirado: false, ReplacedByToken: null);
        var usuario = new UsuarioAutenticadoDto(usuarioId, "user@test.com", "Usuario", EstaBloquado: false);

        _refreshTokens.BuscarPorTokenAsync(Arg.Any<string>()).Returns(tokenDto);
        _usuarios.BuscarPorIdAsync(usuarioId).Returns(usuario);
        _usuarios.ObtenerRolesAsync(usuarioId).Returns(new List<string> { "Tecnico" });
        _tokenService.GenerarAccessToken(usuarioId, "user@test.com", "Usuario", Arg.Any<IReadOnlyList<string>>())
            .Returns(("nuevo.access.token", DateTime.UtcNow.AddMinutes(15)));
        _tokenService.GenerarRefreshToken().Returns("nuevo-refresh-token");

        var command = new RefreshTokenCommand("token-activo", "10.0.0.1");

        var result = await CrearHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("nuevo.access.token");
        result.Value.RefreshToken.Should().Be("nuevo-refresh-token");
        await _refreshTokens.Received(1).RevocarAsync(tokenId, "10.0.0.1", "nuevo-refresh-token", "rotacion");
        await _refreshTokens.Received(1).CrearAsync(usuarioId, "nuevo-refresh-token", "10.0.0.1");
    }
}
