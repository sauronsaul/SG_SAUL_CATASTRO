using FluentAssertions;
using NetTopologySuite.Geometries;
using SG.Domain.Catastro;
using SG.Domain.Catastro.ValueObjects;

namespace SG.Domain.Tests.Catastro;

public sealed class PredioCrearYMetodosTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static UbicacionCatastral UbicacionValida() =>
        UbicacionCatastral.Crear("001", "0001", "0001").Value;

    private static GeometriaPredial GeometriaValida()
    {
        var factory = new GeometryFactory(new PrecisionModel(), GeometriaPredial.SridObligatorio);
        var coords = new[]
        {
            new Coordinate(500000, 8000000),
            new Coordinate(500100, 8000000),
            new Coordinate(500100, 8000100),
            new Coordinate(500000, 8000100),
            new Coordinate(500000, 8000000),
        };
        return GeometriaPredial.Crear(
            factory.CreatePolygon(factory.CreateLinearRing(coords))).Value;
    }

    private static Predio PredioNuevo() =>
        Predio.Crear(UbicacionValida(), 250m, Guid.NewGuid(), UsuarioId).Value;

    // ── Predio.Crear guards ─────────────────────────────────────────────────

    [Fact]
    public void Crear_UbicacionNula_EsFailure_UbicacionRequerida()
    {
        var result = Predio.Crear(null!, 250m, Guid.NewGuid(), UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.UbicacionRequerida);
    }

    [Fact]
    public void Crear_SuperficieCero_EsFailure_SuperficieInvalida()
    {
        var result = Predio.Crear(UbicacionValida(), 0m, Guid.NewGuid(), UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.SuperficieInvalida);
    }

    [Fact]
    public void Crear_SuperficieNegativa_EsFailure_SuperficieInvalida()
    {
        var result = Predio.Crear(UbicacionValida(), -10m, Guid.NewGuid(), UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.SuperficieInvalida);
    }

    [Fact]
    public void Crear_UsoSueloVacio_EsFailure_UsoSueloRequerido()
    {
        var result = Predio.Crear(UbicacionValida(), 250m, Guid.Empty, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.UsoSueloRequerido);
    }

    [Fact]
    public void Crear_UbicacionConTripleteNoNumerico_EsFailure()
    {
        var ubicacion = UbicacionCatastral.Crear("Distrito", "M1", "L1").Value;

        var result = Predio.Crear(ubicacion, 250m, Guid.NewGuid(), UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.TripleteCatastralInvalido);
    }

    [Fact]
    public void Crear_UbicacionConTripleteNumerico_ConservaComponentesCanonicos()
    {
        var result = Predio.Crear(UbicacionValida(), 250m, Guid.NewGuid(), UsuarioId);

        result.IsSuccess.Should().BeTrue();
        result.Value.CodUv.Should().Be(1);
        result.Value.CodMan.Should().Be(1);
        result.Value.CodPred.Should().Be(1);
    }

    // ── Predio.Validar guard ────────────────────────────────────────────────

    [Fact]
    public void Validar_CodigoNulo_EsFailure_CodigoCatastralRequerido()
    {
        var predio = PredioNuevo();
        predio.EnviarARevision(UsuarioId);

        var result = predio.Validar(null!, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.CodigoCatastralRequerido);
    }

    // ── Predio.AsignarCodigoOficial ─────────────────────────────────────────

    [Fact]
    public void AsignarCodigoOficial_CodigoValido_EsExito_ActualizaCodigoCatastral()
    {
        var predio = PredioNuevo();
        var codigo = CodigoCatastral.Crear("02-006-028-001-0001-0001").Value;

        var result = predio.AsignarCodigoOficial(codigo, UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.CodigoCatastral.Should().NotBeNull();
        predio.CodigoCatastral!.Valor.Should().Be("02-006-028-001-0001-0001");
    }

    [Fact]
    public void AsignarCodigoOficial_CodigoNulo_EsFailure_CodigoCatastralRequerido()
    {
        var predio = PredioNuevo();

        var result = predio.AsignarCodigoOficial(null!, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.CodigoCatastralRequerido);
    }

    // ── Predio.AsignarGeometria ─────────────────────────────────────────────

    [Fact]
    public void AsignarGeometria_GeometriaValida_EsExito_GeometriaAsignada()
    {
        var predio = PredioNuevo();
        var geo = GeometriaValida();

        var result = predio.AsignarGeometria(geo, UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.Geometria.Should().NotBeNull();
    }

    [Fact]
    public void AsignarGeometria_GeometriaNula_EsFailure_PoligonoRequerido()
    {
        var predio = PredioNuevo();

        var result = predio.AsignarGeometria(null!, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(GeometriaPredialErrores.PoligonoRequerido);
    }

    // ── Predio.ActualizarSuperficieSig ──────────────────────────────────────

    [Fact]
    public void ActualizarSuperficieSig_ValorPositivo_EsExito_SuperficieActualizada()
    {
        var predio = PredioNuevo();

        var result = predio.ActualizarSuperficieSig(312.5m);

        result.IsSuccess.Should().BeTrue();
        predio.SuperficieSig.Should().Be(312.5m);
    }

    [Fact]
    public void ActualizarSuperficieSig_ValorCero_EsFailure_SuperficieInvalida()
    {
        var predio = PredioNuevo();

        var result = predio.ActualizarSuperficieSig(0m);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.SuperficieInvalida);
    }

    [Fact]
    public void ActualizarSuperficieSig_ValorNegativo_EsFailure_SuperficieInvalida()
    {
        var predio = PredioNuevo();

        var result = predio.ActualizarSuperficieSig(-1m);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.SuperficieInvalida);
    }

    // ── Predio.AsignarSuperficieOficial ─────────────────────────────────────

    [Fact]
    public void AsignarSuperficieOficial_ValorPositivo_EsExito_SuperficieActualizada()
    {
        var predio = PredioNuevo();

        var result = predio.AsignarSuperficieOficial(285m, UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.SuperficieOficial.Should().Be(285m);
    }

    [Fact]
    public void AsignarSuperficieOficial_ValorNegativo_EsFailure_SuperficieInvalida()
    {
        var predio = PredioNuevo();

        var result = predio.AsignarSuperficieOficial(-5m, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.SuperficieInvalida);
    }

    [Fact]
    public void AsignarSuperficieOficial_ValorCero_EsFailure_SuperficieInvalida()
    {
        var predio = PredioNuevo();

        var result = predio.AsignarSuperficieOficial(0m, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.SuperficieInvalida);
    }
}
