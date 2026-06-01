using FluentAssertions;
using SG.Domain.Common;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Domain.Tests.Importacion;

public sealed class ImportacionAggregateTests
{
    private static ImportacionDomain.Importacion CrearImportacionEnPreview(int totalFilas = 100)
        => ImportacionDomain.Importacion.CrearPreview(
               perfilId:       Guid.NewGuid(),
               nombreArchivo:  "test.zip",
               rutaMinioZip:   "imports/test.zip",
               importadoPorId: Guid.NewGuid(),
               totalFilas:     totalFilas);

    // ── 1 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void CrearPreview_ConDatosValidos_ProduceEstadoPreviewGenerado()
    {
        var importacion = CrearImportacionEnPreview(totalFilas: 500);

        importacion.Estado.Should().Be(ImportacionDomain.EstadoImportacion.PreviewGenerado);
        importacion.TotalFilas.Should().Be(500);
        importacion.FilasImportadas.Should().Be(0);
        importacion.FilasOmitidas.Should().Be(0);
        importacion.FilasRechazadas.Should().Be(0);
        importacion.FilasConAdvertencia.Should().Be(0);
    }

    // ── 2 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RegistrarConteosPreview_EnEstadoPreviewGenerado_EscribeCampos()
    {
        var importacion = CrearImportacionEnPreview();

        importacion.RegistrarConteosPreview(
            filasACrear:         30,
            filasAActualizar:    50,
            filasAOmitir:        10,
            filasRechazadas:     10,
            filasConAdvertencia:  5);

        importacion.FilasImportadas.Should().Be(80);     // 30 + 50
        importacion.FilasOmitidas.Should().Be(10);
        importacion.FilasRechazadas.Should().Be(10);
        importacion.FilasConAdvertencia.Should().Be(5);
        importacion.Estado.Should().Be(ImportacionDomain.EstadoImportacion.PreviewGenerado);
    }

    // ── 3 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RegistrarConteosPreview_EnEstadoConfirmada_LanzaDomainException()
    {
        var importacion = CrearImportacionEnPreview();
        importacion.RegistrarConteosPreview(10, 40, 0, 0, 0);
        importacion.Confirmar();

        var act = () => importacion.RegistrarConteosPreview(10, 40, 0, 0, 0);

        act.Should().Throw<DomainException>()
            .WithMessage("*PreviewGenerado*");
    }

    // ── 4 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RegistrarConteosPreview_EnEstadoFallida_LanzaDomainException()
    {
        var importacion = CrearImportacionEnPreview();
        importacion.MarcarFallida();

        var act = () => importacion.RegistrarConteosPreview(10, 40, 0, 0, 0);

        act.Should().Throw<DomainException>()
            .WithMessage("*PreviewGenerado*");
    }

    // ── 5 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RegistrarConteosConfirmacion_EnEstadoPreviewGenerado_EscribeCampos()
    {
        var importacion = CrearImportacionEnPreview();

        importacion.RegistrarConteosConfirmacion(
            filasCreadas:        20,
            filasActualizadas:   70,
            filasOmitidas:        5,
            filasRechazadas:      5,
            filasConAdvertencia:  3);

        importacion.FilasImportadas.Should().Be(90);     // 20 + 70
        importacion.FilasOmitidas.Should().Be(5);
        importacion.FilasRechazadas.Should().Be(5);
        importacion.FilasConAdvertencia.Should().Be(3);
        importacion.Estado.Should().Be(ImportacionDomain.EstadoImportacion.PreviewGenerado);
    }

    // ── 6 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RegistrarConteosConfirmacion_EnEstadoConfirmada_LanzaDomainException()
    {
        var importacion = CrearImportacionEnPreview();
        importacion.RegistrarConteosConfirmacion(50, 50, 0, 0, 0);
        importacion.Confirmar();

        var act = () => importacion.RegistrarConteosConfirmacion(50, 50, 0, 0, 0);

        act.Should().Throw<DomainException>()
            .WithMessage("*PreviewGenerado*");
    }

    // ── 7 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Confirmar_DesdePreviewGenerado_TransicionaAConfirmada()
    {
        var importacion = CrearImportacionEnPreview();

        importacion.Confirmar();

        importacion.Estado.Should().Be(ImportacionDomain.EstadoImportacion.Confirmada);
    }

    // ── 8 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Confirmar_DesdeEstadoConfirmada_LanzaDomainException()
    {
        var importacion = CrearImportacionEnPreview();
        importacion.Confirmar();

        var act = () => importacion.Confirmar();

        act.Should().Throw<DomainException>()
            .WithMessage("*PreviewGenerado*");
    }

    // ── 9 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MarcarFallida_DesdePreviewGenerado_TransicionaAFallida()
    {
        var importacion = CrearImportacionEnPreview();

        importacion.MarcarFallida();

        importacion.Estado.Should().Be(ImportacionDomain.EstadoImportacion.Fallida);
    }

    // ── 10 ────────────────────────────────────────────────────────────────

    [Fact]
    public void MarcarFallida_DesdeEstadoConfirmada_LanzaDomainException()
    {
        var importacion = CrearImportacionEnPreview();
        importacion.Confirmar();

        var act = () => importacion.MarcarFallida();

        act.Should().Throw<DomainException>()
            .WithMessage("*PreviewGenerado*");
    }
}
