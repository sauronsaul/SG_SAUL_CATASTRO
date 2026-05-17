using FluentAssertions;
using NSubstitute;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Autenticacion;
using SG.Application.Autenticacion;
using SG.Application.Autenticacion.UsuarioActual;

namespace SG.Application.Tests.Autenticacion;

public sealed class ObtenerUsuarioActualQueryHandlerTests
{
    private readonly IUsuarioServicio _usuarios = Substitute.For<IUsuarioServicio>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private ObtenerUsuarioActualQueryHandler CrearHandler() =>
        new(_usuarios, _currentUser);

    [Fact]
    public async Task Handle_SinUsuarioEnContexto_RetornaUsuarioNoEncontrado()
    {
        _currentUser.UserId.Returns((Guid?)null);
        var query = new ObtenerUsuarioActualQuery();

        var result = await CrearHandler().Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AutenticacionErrores.UsuarioNoEncontrado);
    }

    [Fact]
    public async Task Handle_UsuarioNoExisteEnBD_RetornaUsuarioNoEncontrado()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId.Returns(userId);
        _usuarios.BuscarPorIdAsync(userId, Arg.Any<CancellationToken>())
            .Returns((UsuarioAutenticadoDto?)null);
        var query = new ObtenerUsuarioActualQuery();

        var result = await CrearHandler().Handle(query, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AutenticacionErrores.UsuarioNoEncontrado);
    }

    [Fact]
    public async Task Handle_UsuarioExiste_RetornaDtoConRoles()
    {
        var userId = Guid.NewGuid();
        var usuario = new UsuarioAutenticadoDto(userId, "admin@test.com", "Administrador", EstaBloquado: false);
        _currentUser.UserId.Returns(userId);
        _usuarios.BuscarPorIdAsync(userId, Arg.Any<CancellationToken>()).Returns(usuario);
        _usuarios.ObtenerRolesAsync(userId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<string>)new List<string> { "Admin" });
        var query = new ObtenerUsuarioActualQuery();

        var result = await CrearHandler().Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("admin@test.com");
        result.Value.NombreCompleto.Should().Be("Administrador");
        result.Value.Roles.Should().ContainSingle(r => r == "Admin");
    }
}
