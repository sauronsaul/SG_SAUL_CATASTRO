using FluentAssertions;
using SG.Domain.Usuarios;

namespace SG.Domain.Tests.Usuarios;

public sealed class UsuarioErroresTests
{
    [Fact]
    public void ErroresDeUsuario_TienenCodigosEsperados()
    {
        UsuarioErrores.EmailRequerido.Code.Should().Be("Usuario.EmailRequerido");
        UsuarioErrores.EmailInvalido.Code.Should().Be("Usuario.EmailInvalido");
        UsuarioErrores.NombreVacio.Code.Should().Be("Usuario.NombreVacio");
        UsuarioErrores.EmailDuplicado.Code.Should().Be("Usuario.EmailDuplicado");
    }
}
