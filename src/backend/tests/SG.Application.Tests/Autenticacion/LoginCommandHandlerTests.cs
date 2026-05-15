using FluentAssertions;
using NSubstitute;
using SG.Application.Abstractions.Autenticacion;
using SG.Application.Autenticacion;
using SG.Application.Autenticacion.Login;

namespace SG.Application.Tests.Autenticacion;

public sealed class LoginCommandHandlerTests
{
    private readonly IUsuarioServicio _usuarios = Substitute.For<IUsuarioServicio>();
    private readonly IRefreshTokenRepositorio _refreshTokens = Substitute.For<IRefreshTokenRepositorio>();
    private readonly ITokenService _tokenService = Substitute.For<ITokenService>();
    private readonly IAuditoriaService _auditoria = Substitute.For<IAuditoriaService>();

    private LoginCommandHandler CrearHandler() =>
        new(_usuarios, _refreshTokens, _tokenService, _auditoria);

    [Fact]
    public async Task Handle_UsuarioNoExiste_RetornaCredencialesInvalidas()
    {
        _usuarios.BuscarPorEmailAsync(Arg.Any<string>()).Returns((UsuarioAutenticadoDto?)null);
        var command = new LoginCommand("noexiste@test.com", "password123", "127.0.0.1");

        var result = await CrearHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AutenticacionErrores.CredencialesInvalidas);
    }

    [Fact]
    public async Task Handle_CuentaBloqueada_RetornaCuentaBloqueada()
    {
        var usuario = new UsuarioAutenticadoDto(Guid.NewGuid(), "admin@test.com", "Admin", EstaBloquado: true);
        _usuarios.BuscarPorEmailAsync("admin@test.com").Returns(usuario);
        var command = new LoginCommand("admin@test.com", "cualquier", "127.0.0.1");

        var result = await CrearHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AutenticacionErrores.CuentaBloqueada);
    }

    [Fact]
    public async Task Handle_PasswordIncorrecto_RegistraFalloYRetornaCredencialesInvalidas()
    {
        var userId = Guid.NewGuid();
        var usuario = new UsuarioAutenticadoDto(userId, "admin@test.com", "Admin", EstaBloquado: false);
        _usuarios.BuscarPorEmailAsync("admin@test.com").Returns(usuario);
        _usuarios.VerificarPasswordAsync(userId, Arg.Any<string>()).Returns(false);
        var command = new LoginCommand("admin@test.com", "wrongpass", "127.0.0.1");

        var result = await CrearHandler().Handle(command, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AutenticacionErrores.CredencialesInvalidas);
        await _usuarios.Received(1).RegistrarAccesoFallidoAsync(userId);
    }

    [Fact]
    public async Task Handle_CredencialesCorrectas_RetornaLoginResponse()
    {
        var userId = Guid.NewGuid();
        var usuario = new UsuarioAutenticadoDto(userId, "admin@test.com", "Admin", EstaBloquado: false);
        _usuarios.BuscarPorEmailAsync("admin@test.com").Returns(usuario);
        _usuarios.VerificarPasswordAsync(userId, "Password!123").Returns(true);
        _usuarios.ObtenerRolesAsync(userId).Returns(new List<string> { "Admin" });
        _tokenService.GenerarAccessToken(userId, "admin@test.com", "Admin", Arg.Any<IReadOnlyList<string>>())
            .Returns(("token.jwt.test", DateTime.UtcNow.AddMinutes(15)));
        _tokenService.GenerarRefreshToken().Returns("refresh-token-seguro");
        var command = new LoginCommand("admin@test.com", "Password!123", "127.0.0.1");

        var result = await CrearHandler().Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("token.jwt.test");
        result.Value.RefreshToken.Should().Be("refresh-token-seguro");
        result.Value.Usuario.Email.Should().Be("admin@test.com");
        await _refreshTokens.Received(1).CrearAsync(userId, "refresh-token-seguro", "127.0.0.1");
    }
}
