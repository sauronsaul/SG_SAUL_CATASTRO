using FluentAssertions;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;
using SG.Domain.Catastro.ValueObjects;

namespace SG.Domain.Tests.Catastro;

public sealed class PredioAsignarPropietarioTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();
    private static readonly DateOnly Hoy = DateOnly.FromDateTime(DateTime.Today);

    private static Predio PredioNuevo()
    {
        var ubicacion = UbicacionCatastral.Crear("001", "0001", "0001").Value;
        return Predio.Crear(ubicacion, 250m, Guid.NewGuid(), UsuarioId).Value;
    }

    [Fact]
    public void AsignarPropietario_NuevoSinRelaciones_EsExito_AgregaRelacion()
    {
        var predio = PredioNuevo();
        var propietarioId = Guid.NewGuid();

        var result = predio.AsignarPropietario(propietarioId, TipoDerecho.Propietario, 100m, Hoy, UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.Relaciones.Should().HaveCount(1);
        predio.Relaciones.Single().PropietarioId.Should().Be(propietarioId);
        predio.Relaciones.Single().EsVigente.Should().BeTrue();
    }

    [Fact]
    public void AsignarPropietario_MismoPropietarioYaVigente_EsFailure_PropietarioYaVigente()
    {
        var predio = PredioNuevo();
        var propietarioId = Guid.NewGuid();
        predio.AsignarPropietario(propietarioId, TipoDerecho.Propietario, 50m, Hoy, UsuarioId);

        var result = predio.AsignarPropietario(propietarioId, TipoDerecho.Poseedor, 10m, Hoy, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RelacionErrores.PropietarioYaVigente);
    }

    [Fact]
    public void AsignarPropietario_SumaPorcentajeSuperaLimite_EsFailure()
    {
        var predio = PredioNuevo();
        predio.AsignarPropietario(Guid.NewGuid(), TipoDerecho.Propietario, 60m, Hoy, UsuarioId);

        // Intentar agregar 50% cuando ya hay 60% vigente → 110% > 100%
        var result = predio.AsignarPropietario(Guid.NewGuid(), TipoDerecho.Poseedor, 50m, Hoy, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RelacionErrores.SumaPorcentajeSuperaLimite);
    }

    [Fact]
    public void AsignarPropietario_PorcentajeCero_EsFailure_PorcentajeInvalido()
    {
        var predio = PredioNuevo();

        var result = predio.AsignarPropietario(Guid.NewGuid(), TipoDerecho.Propietario, 0m, Hoy, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RelacionErrores.PorcentajeInvalido);
    }

    [Fact]
    public void AsignarPropietario_PorcentajeNegativo_EsFailure_PorcentajeInvalido()
    {
        // Porcentaje negativo bypasa el chequeo de suma del agregado (suma + negativo < 100)
        // y llega a RelacionPredioPropietario.Crear que valida porcentaje <= 0.
        var predio = PredioNuevo();

        var result = predio.AsignarPropietario(Guid.NewGuid(), TipoDerecho.Propietario, -5m, Hoy, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RelacionErrores.PorcentajeInvalido);
    }

    [Fact]
    public void CerrarRelacion_PropietarioVigente_EsExito_RelacionYaNoVigente()
    {
        var predio = PredioNuevo();
        var propietarioId = Guid.NewGuid();
        predio.AsignarPropietario(propietarioId, TipoDerecho.Propietario, 100m, Hoy, UsuarioId);

        var result = predio.CerrarRelacionPropietario(propietarioId, Hoy, UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.Relaciones.Single().EsVigente.Should().BeFalse();
    }

    [Fact]
    public void CerrarRelacion_FechaCierreAnteriorAInicio_EsFailure()
    {
        var predio = PredioNuevo();
        var propietarioId = Guid.NewGuid();
        var inicio = new DateOnly(2026, 1, 1);
        predio.AsignarPropietario(propietarioId, TipoDerecho.Propietario, 100m, inicio, UsuarioId);

        var fechaAntes = inicio.AddDays(-1);
        var result = predio.CerrarRelacionPropietario(propietarioId, fechaAntes, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RelacionErrores.FechaCierreAnteriorAInicio);
    }

    [Fact]
    public void CerrarRelacion_PropietarioCerrado_EsFailure_YaCerrada()
    {
        var predio = PredioNuevo();
        var propietarioId = Guid.NewGuid();
        predio.AsignarPropietario(propietarioId, TipoDerecho.Propietario, 100m, Hoy, UsuarioId);
        predio.CerrarRelacionPropietario(propietarioId, Hoy, UsuarioId);

        // Intentar cerrar de nuevo
        var result = predio.CerrarRelacionPropietario(propietarioId, Hoy, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RelacionErrores.YaCerrada);
    }

    [Fact]
    public void AsignarPropietario_DosDistintosSinSuperarLimite_EsExito()
    {
        var predio = PredioNuevo();
        var p1 = Guid.NewGuid();
        var p2 = Guid.NewGuid();

        predio.AsignarPropietario(p1, TipoDerecho.Propietario, 60m, Hoy, UsuarioId);
        var result = predio.AsignarPropietario(p2, TipoDerecho.Poseedor, 40m, Hoy, UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.Relaciones.Should().HaveCount(2);
        predio.Relaciones.All(r => r.EsVigente).Should().BeTrue();
    }
}
