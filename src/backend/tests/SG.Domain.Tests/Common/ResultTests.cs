using FluentAssertions;
using SG.Domain.Common;

namespace SG.Domain.Tests.Common;

public sealed class ResultTests
{
    private sealed class ResultTestable : Result
    {
        public ResultTestable(bool isSuccess, DomainError error) : base(isSuccess, error) { }
    }

    [Fact]
    public void Success_IsSuccess_EsVerdadero()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
    }

    [Fact]
    public void Failure_IsFailure_EsVerdadero()
    {
        var error = new DomainError("Test.Error", "Error de prueba");

        var result = Result.Failure(error);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void SuccessGenerico_RetornaValor()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void FailureGenerico_AccederValor_LanzaInvalidOperationException()
    {
        var result = Result.Failure<int>(new DomainError("Test.Error", "Error"));

        var acceso = () => result.Value;

        acceso.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void FailureConCodigoYMensaje_CreaErrorCorrecto()
    {
        var result = Result.Failure("Mi.Codigo", "Mi mensaje");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Mi.Codigo");
        result.Error.Message.Should().Be("Mi mensaje");
    }

    [Fact]
    public void Guard_ExitoConError_LanzaInvalidOperationException()
    {
        var error = new DomainError("Test.Error", "Error");

        var acto = () => new ResultTestable(true, error);

        acto.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Guard_FalloSinError_LanzaInvalidOperationException()
    {
        var acto = () => new ResultTestable(false, DomainError.None);

        acto.Should().Throw<InvalidOperationException>();
    }
}
