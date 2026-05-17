using FluentAssertions;
using SG.Application.Autenticacion.Login;

namespace SG.Application.Tests.Autenticacion;

public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validar_EmailVacio_EsInvalido()
    {
        var command = new LoginCommand("", "Password!123", "127.0.0.1");

        var resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validar_EmailSinFormato_EsInvalido()
    {
        var command = new LoginCommand("noesuncorreo", "Password!123", "127.0.0.1");

        var resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Validar_PasswordVacio_EsInvalido()
    {
        var command = new LoginCommand("admin@test.com", "", "127.0.0.1");

        var resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == "Password");
    }

    [Fact]
    public void Validar_DatosCorrectos_EsValido()
    {
        var command = new LoginCommand("admin@municipio.gob.bo", "Password!123", "127.0.0.1");

        var resultado = _validator.Validate(command);

        resultado.IsValid.Should().BeTrue();
    }
}
