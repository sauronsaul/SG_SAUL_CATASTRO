using FluentAssertions;
using SG.Domain.Usuarios;

namespace SG.Domain.Tests.Usuarios;

public sealed class UsuarioTests
{
    [Fact]
    public void Crear_EmailNulo_RetornaEmailRequerido()
    {
        var result = Usuario.Crear(null, "Administrador");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UsuarioErrores.EmailRequerido);
    }

    [Fact]
    public void Crear_EmailSoloEspacios_RetornaEmailRequerido()
    {
        var result = Usuario.Crear("   ", "Administrador");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UsuarioErrores.EmailRequerido);
    }

    [Fact]
    public void Crear_EmailSinArroba_RetornaEmailInvalido()
    {
        var result = Usuario.Crear("sindoblearroba.com", "Administrador");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UsuarioErrores.EmailInvalido);
    }

    [Fact]
    public void Crear_NombreNulo_RetornaNombreVacio()
    {
        var result = Usuario.Crear("admin@municipio.gob.bo", null);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UsuarioErrores.NombreVacio);
    }

    [Fact]
    public void Crear_DatosValidos_RetornaUsuarioConEmailNormalizado()
    {
        var result = Usuario.Crear("admin@municipio.gob.bo", "Administrador");

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("admin@municipio.gob.bo");
        result.Value.EmailNormalizado.Should().Be("ADMIN@MUNICIPIO.GOB.BO");
        result.Value.NombreCompleto.Should().Be("Administrador");
    }

    [Fact]
    public void Crear_EmailConEspaciosExternos_LoRecorta()
    {
        var result = Usuario.Crear("  admin@test.com  ", "Administrador");

        result.IsSuccess.Should().BeTrue();
        result.Value.Email.Should().Be("admin@test.com");
    }

    [Fact]
    public void Crear_EmailMayorA320Caracteres_RetornaEmailInvalido()
    {
        var local = new string('a', 312);
        var emailLargo = $"{local}@test.com"; // 312 + 9 = 321 chars

        var result = Usuario.Crear(emailLargo, "Administrador");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UsuarioErrores.EmailInvalido);
    }

    [Fact]
    public void Crear_NombreMayorA200Caracteres_RetornaNombreVacio()
    {
        var nombreLargo = new string('A', 201);

        var result = Usuario.Crear("admin@test.com", nombreLargo);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(UsuarioErrores.NombreVacio);
    }
}
