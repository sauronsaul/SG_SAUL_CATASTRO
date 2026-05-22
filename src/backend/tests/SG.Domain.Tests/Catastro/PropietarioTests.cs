using FluentAssertions;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;

namespace SG.Domain.Tests.Catastro;

public sealed class PropietarioTests
{
    [Fact]
    public void CrearPersonaNatural_DatosValidos_EsExito()
    {
        var result = Propietario.CrearPersonaNatural("Juan", "Pérez García", "12345678");

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoPropietario.PersonaNatural);
        result.Value.Nombre.Should().Be("Juan");
        result.Value.Apellidos.Should().Be("Pérez García");
        result.Value.Cedula.Should().Be("12345678");
        result.Value.NombreCompleto.Should().Be("Juan Pérez García");
    }

    [Fact]
    public void CrearPersonaNatural_SinNombre_EsFailure()
    {
        var result = Propietario.CrearPersonaNatural("", "Pérez García", "12345678");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PropietarioErrores.NombreRequerido);
    }

    [Fact]
    public void CrearPersonaNatural_NombreSoloEspacios_EsFailure()
    {
        var result = Propietario.CrearPersonaNatural("   ", "Pérez García", "12345678");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PropietarioErrores.NombreRequerido);
    }

    [Fact]
    public void CrearPersonaNatural_SinApellidos_EsFailure()
    {
        var result = Propietario.CrearPersonaNatural("Juan", "", "12345678");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PropietarioErrores.ApellidosRequeridos);
    }

    [Fact]
    public void CrearPersonaNatural_SinCedula_EsFailure()
    {
        var result = Propietario.CrearPersonaNatural("Juan", "Pérez García", "");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PropietarioErrores.CedulaRequerida);
    }

    [Fact]
    public void CrearPersonaNatural_CedulaDemasiada_EsFailure()
    {
        var cedulaLarga = new string('1', 16);

        var result = Propietario.CrearPersonaNatural("Juan", "Pérez García", cedulaLarga);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PropietarioErrores.CedulaInvalida);
    }

    [Fact]
    public void CrearPersonaJuridica_DatosValidos_EsExito()
    {
        var result = Propietario.CrearPersonaJuridica("Empresa SRL", "1234567890");

        result.IsSuccess.Should().BeTrue();
        result.Value.Tipo.Should().Be(TipoPropietario.PersonaJuridica);
        result.Value.RazonSocial.Should().Be("Empresa SRL");
        result.Value.Nit.Should().Be("1234567890");
        result.Value.NombreCompleto.Should().Be("Empresa SRL");
    }

    [Fact]
    public void CrearPersonaJuridica_SinRazonSocial_EsFailure()
    {
        var result = Propietario.CrearPersonaJuridica("", "1234567890");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PropietarioErrores.RazonSocialRequerida);
    }

    [Fact]
    public void CrearPersonaJuridica_SinNit_EsFailure()
    {
        var result = Propietario.CrearPersonaJuridica("Empresa SRL", "");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PropietarioErrores.NitRequerido);
    }

    [Fact]
    public void CrearPersonaJuridica_NitNoNumerico_EsFailure()
    {
        var result = Propietario.CrearPersonaJuridica("Empresa SRL", "ABC123");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PropietarioErrores.NitInvalido);
    }

    [Fact]
    public void CrearPersonaJuridica_NitMasDe13Digitos_EsFailure()
    {
        var result = Propietario.CrearPersonaJuridica("Empresa SRL", "12345678901234");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PropietarioErrores.NitInvalido);
    }

    [Fact]
    public void CrearPersonaNatural_DatosOpcionales_SeAlmacenan()
    {
        var result = Propietario.CrearPersonaNatural(
            "Juan", "Pérez García", "12345678",
            email: "juan@test.com", telefono: "70012345", direccion: "Calle 1");

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("juan@test.com");
        result.Value.Telefono.Should().Be("70012345");
        result.Value.Direccion.Should().Be("Calle 1");
    }
}
