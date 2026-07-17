using FluentAssertions;
using SG.Contracts.Autenticacion;
using SG.Web.Services;

namespace SG.Web.Tests;

public sealed class EstadoVisorTests
{
    [Fact]
    public void CambiarVisibilidad_SoloAfectaLaCapaIndicada()
    {
        var estado = new EstadoVisor();
        estado.ConfigurarMunicipio(["parcelas", "edificaciones"]);

        estado.CambiarVisibilidad("parcelas", false).Should().BeTrue();

        estado.EsVisible("parcelas").Should().BeFalse();
        estado.EsVisible("edificaciones").Should().BeTrue();
        estado.CambiarVisibilidad("capa-inexistente", false).Should().BeFalse();
    }

    [Fact]
    public void Multiples401_GeneranUnaSolaSolicitudDeLogin()
    {
        var sesion = new SesionAutenticacion();
        sesion.Iniciar(new LoginResponse(
            "access-token",
            DateTime.UtcNow.AddMinutes(15),
            "refresh-token",
            new UsuarioDto(Guid.NewGuid(), "admin@uyuni.bo", "Admin", ["Admin"])));

        sesion.NotificarNoAutorizado().Should().BeTrue();
        sesion.NotificarNoAutorizado().Should().BeFalse();
        sesion.NotificarNoAutorizado().Should().BeFalse();
        sesion.RequiereAutenticacion.Should().BeTrue();
        sesion.SesionExpiro.Should().BeTrue();
    }
}
