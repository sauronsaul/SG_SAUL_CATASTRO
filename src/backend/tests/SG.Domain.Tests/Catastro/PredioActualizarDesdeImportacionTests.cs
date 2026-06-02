using FluentAssertions;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;
using SG.Domain.Catastro.ValueObjects;

namespace SG.Domain.Tests.Catastro;

public sealed class PredioActualizarDesdeImportacionTests
{
    private static readonly Guid UsuarioId = Guid.NewGuid();

    private static UbicacionCatastral UbicacionValida() =>
        UbicacionCatastral.Crear("001", "0001", "0001").Value;

    // ── estados permitidos ────────────────────────────────────────────────

    [Fact]
    public void ActualizarDesdeImportacion_PredioImportado_ActualizaCamposEsperados()
    {
        var predio = Predio.CrearImportado(UbicacionValida(), 200m, UsuarioId).Value;

        var result = predio.ActualizarDesdeImportacion(
            superficieDeclarada:   350m,
            importadoPor:          UsuarioId,
            propietarioReferencia: "Juan Quispe",
            tipoInmuebleOrigen:    "Residencial",
            codigoOrigen:          "ORG-007",
            requiereRevision:      true,
            detalleRevision:       "Coordenadas fuera de límite");

        result.IsSuccess.Should().BeTrue();
        predio.SuperficieDeclarada.Should().Be(350m);
        predio.PropietarioReferencia.Should().Be("Juan Quispe");
        predio.TipoInmuebleOrigen.Should().Be("Residencial");
        predio.CodigoOrigen.Should().Be("ORG-007");
        predio.RequiereRevision.Should().BeTrue();
        predio.DetalleRevision.Should().Be("Coordenadas fuera de límite");
    }

    [Fact]
    public void ActualizarDesdeImportacion_PredioBorrador_EsExito_ActualizaSuperficie()
    {
        var predio = Predio.Crear(UbicacionValida(), 200m, Guid.NewGuid(), UsuarioId).Value;

        var result = predio.ActualizarDesdeImportacion(300m, UsuarioId);

        result.IsSuccess.Should().BeTrue();
        predio.SuperficieDeclarada.Should().Be(300m);
    }

    // ── campos que el técnico pudo haber editado no se pisan ──────────────

    [Fact]
    public void ActualizarDesdeImportacion_NoModificaUsoSueloNiCodigoCatastral()
    {
        var usoSueloId = Guid.NewGuid();
        var predio = Predio.Crear(UbicacionValida(), 200m, usoSueloId, UsuarioId).Value;
        var codigo = CodigoCatastral.Crear("02-006-028-001-0001-0001").Value;
        predio.AsignarCodigoOficial(codigo, UsuarioId);

        predio.ActualizarDesdeImportacion(
            superficieDeclarada:   999m,
            importadoPor:          UsuarioId,
            propietarioReferencia: "Nuevo propietario",
            codigoOrigen:          "XX");

        predio.UsoSueloId.Should().Be(usoSueloId);
        predio.CodigoCatastral!.Valor.Should().Be("02-006-028-001-0001-0001");
    }

    // ── estados prohibidos (Validado, EnRevision, Observado) ──────────────

    [Theory]
    [InlineData(EstadoPredio.EnRevision)]
    [InlineData(EstadoPredio.Validado)]
    [InlineData(EstadoPredio.Observado)]
    public void ActualizarDesdeImportacion_EstadoProtegido_RetornaEstadoNoPermiteReimportacion(
        EstadoPredio estado)
    {
        var predio = CrearPredioEnEstado(estado);

        var result = predio.ActualizarDesdeImportacion(100m, UsuarioId);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PredioErrores.EstadoNoPermiteReimportacion);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static Predio CrearPredioEnEstado(EstadoPredio estado)
    {
        switch (estado)
        {
            case EstadoPredio.Importado:
                return Predio.CrearImportado(UbicacionValida(), 200m, UsuarioId).Value;

            case EstadoPredio.Borrador:
                return Predio.Crear(UbicacionValida(), 200m, Guid.NewGuid(), UsuarioId).Value;

            case EstadoPredio.EnRevision:
            {
                var p = Predio.Crear(UbicacionValida(), 200m, Guid.NewGuid(), UsuarioId).Value;
                p.EnviarARevision(UsuarioId);
                return p;
            }

            case EstadoPredio.Validado:
            {
                var p = Predio.Crear(UbicacionValida(), 200m, Guid.NewGuid(), UsuarioId).Value;
                p.EnviarARevision(UsuarioId);
                p.Validar(CodigoCatastral.Crear("02-006-028-001-0001-0001").Value, UsuarioId);
                return p;
            }

            case EstadoPredio.Observado:
            {
                var p = Predio.Crear(UbicacionValida(), 200m, Guid.NewGuid(), UsuarioId).Value;
                p.EnviarARevision(UsuarioId);
                p.Observar("Predio observado para test.", UsuarioId);
                return p;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(estado), estado, null);
        }
    }
}
