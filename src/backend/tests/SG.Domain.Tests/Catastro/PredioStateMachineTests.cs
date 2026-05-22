using FluentAssertions;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;
using SG.Domain.Catastro.ValueObjects;

namespace SG.Domain.Tests.Catastro;

public sealed class PredioStateMachineTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static Predio PredioEnBorrador()
    {
        var ubicacion = UbicacionCatastral.Crear("001", "0001", "0001").Value;
        return Predio.Crear(ubicacion, 250m, Guid.NewGuid(), UsuarioId).Value;
    }

    private static Predio PredioEnRevision()
    {
        var p = PredioEnBorrador();
        p.EnviarARevision(UsuarioId);
        return p;
    }

    private static Predio PredioObservado()
    {
        var p = PredioEnRevision();
        p.Observar("Faltan documentos.", UsuarioId);
        return p;
    }

    [Fact]
    public void EnviarARevision_DesdeBorrador_EsExito_EstadoCambia()
    {
        var predio = PredioEnBorrador();

        var result = predio.EnviarARevision(UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.Estado.Should().Be(EstadoPredio.EnRevision);
    }

    [Fact]
    public void EnviarARevision_DesdeValidado_EsFailure_TransicionInvalida()
    {
        var predio = PredioEnRevision();
        var codigo = CodigoCatastral.Crear("02-006-028-001-0001-0001").Value;
        predio.Validar(codigo, UsuarioId);

        var result = predio.EnviarARevision(UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("Predio.TransicionInvalida");
    }

    [Fact]
    public void Validar_DesdeEnRevision_EsExito_EstadoCambia()
    {
        var predio = PredioEnRevision();
        var codigo = CodigoCatastral.Crear("02-006-028-001-0001-0001").Value;

        var result = predio.Validar(codigo, UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.Estado.Should().Be(EstadoPredio.Validado);
        predio.CodigoCatastral.Should().NotBeNull();
        predio.CodigoCatastral!.Valor.Should().Be("02-006-028-001-0001-0001");
    }

    [Fact]
    public void Validar_DesdeBorrador_EsFailure_TransicionInvalida()
    {
        var predio = PredioEnBorrador();
        var codigo = CodigoCatastral.Crear("02-006-028-001-0001-0001").Value;

        var result = predio.Validar(codigo, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("Predio.TransicionInvalida");
    }

    [Fact]
    public void Observar_DesdeEnRevision_ConMotivo_EsExito()
    {
        var predio = PredioEnRevision();

        var result = predio.Observar("Falta plano catastral.", UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.Estado.Should().Be(EstadoPredio.Observado);
    }

    [Fact]
    public void Observar_SinMotivo_EsFailure()
    {
        var predio = PredioEnRevision();

        var result = predio.Observar("", UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.ObservacionesRequeridas);
    }

    [Fact]
    public void Observar_MotivoSoloEspacios_EsFailure()
    {
        var predio = PredioEnRevision();

        var result = predio.Observar("   ", UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.ObservacionesRequeridas);
    }

    [Fact]
    public void RetornarBorrador_DesdeObservado_EsExito()
    {
        var predio = PredioObservado();

        var result = predio.RetornarBorrador(UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.Estado.Should().Be(EstadoPredio.Borrador);
    }

    [Fact]
    public void RetornarBorrador_DesdeEnRevision_EsFailure_TransicionInvalida()
    {
        var predio = PredioEnRevision();

        var result = predio.RetornarBorrador(UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().StartWith("Predio.TransicionInvalida");
    }

    [Fact]
    public void EnviarARevision_AgregaRegistroAlHistorial()
    {
        var predio = PredioEnBorrador();

        predio.EnviarARevision(UsuarioId);

        predio.Historial.Should().HaveCount(1);
        predio.Historial.Single().EstadoAnterior.Should().Be(EstadoPredio.Borrador);
        predio.Historial.Single().EstadoNuevo.Should().Be(EstadoPredio.EnRevision);
    }

    [Fact]
    public void Validar_AgregaRegistroAlHistorial()
    {
        var predio = PredioEnRevision();
        var codigo = CodigoCatastral.Crear("02-006-028-001-0001-0001").Value;

        predio.Validar(codigo, UsuarioId);

        // EnviarARevision ya agregó 1, Validar agrega 1 más
        predio.Historial.Should().HaveCount(2);
        predio.Historial.Last().EstadoAnterior.Should().Be(EstadoPredio.EnRevision);
        predio.Historial.Last().EstadoNuevo.Should().Be(EstadoPredio.Validado);
    }

    [Fact]
    public void Observar_AgregaRegistroConObservaciones()
    {
        var predio = PredioEnRevision();
        const string motivo = "Documentación incompleta.";

        predio.Observar(motivo, UsuarioId);

        predio.Historial.Last().EstadoNuevo.Should().Be(EstadoPredio.Observado);
        predio.Historial.Last().Observaciones.Should().Be(motivo);
    }

    [Fact]
    public void RetornarBorrador_AgregaRegistroAlHistorial()
    {
        var predio = PredioObservado();

        predio.RetornarBorrador(UsuarioId);

        predio.Historial.Should().HaveCount(3); // EnRevision + Observado + Borrador
        predio.Historial.Last().EstadoNuevo.Should().Be(EstadoPredio.Borrador);
    }
}
