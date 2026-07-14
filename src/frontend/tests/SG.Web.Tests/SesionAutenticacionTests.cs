using FluentAssertions;
using SG.Contracts.Autenticacion;
using SG.Web.Services;

namespace SG.Web.Tests;

public sealed class SesionAutenticacionTests
{
    [Fact]
    public void LoginExitoso_ConservaAccessTokenYExpiracion()
    {
        var sesion = new SesionAutenticacion();
        var respuesta = CrearRespuesta();

        sesion.Iniciar(respuesta);

        sesion.AccessToken.Should().Be("access-token-prueba");
        sesion.ExpiresAt.Should().Be(respuesta.ExpiresAt);
        sesion.RequiereAutenticacion.Should().BeFalse();
    }

    [Fact]
    public void LoginExitoso_NoConservaRefreshToken()
    {
        var sesion = new SesionAutenticacion();

        sesion.Iniciar(CrearRespuesta());

        sesion.GetType().GetProperty("RefreshToken").Should().BeNull();
        sesion.GetType().GetFields(
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic)
            .Select(x => x.Name)
            .Should().NotContain(x => x.Contains("refresh", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TokenExpirado_RequiereNuevaAutenticacion()
    {
        var sesion = new SesionAutenticacion();
        sesion.Iniciar(CrearRespuesta(DateTime.UtcNow.AddMinutes(-1)));

        sesion.EstaExpirada(DateTime.UtcNow).Should().BeTrue();
    }

    private static LoginResponse CrearRespuesta(DateTime? expiracion = null) => new(
        "access-token-prueba",
        expiracion ?? DateTime.UtcNow.AddMinutes(15),
        "refresh-token-que-no-debe-persistirse",
        new UsuarioDto(Guid.NewGuid(), "tecnico@uyuni.bo", "Técnico Uyuni", ["Tecnico"]));
}
