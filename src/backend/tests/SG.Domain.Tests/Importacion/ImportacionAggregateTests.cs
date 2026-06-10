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
        // Todos los conteos inician en cero
        importacion.FilasEstimadasACrear.Should().Be(0);
        importacion.FilasEstimadasAActualizar.Should().Be(0);
        importacion.FilasEstimadasAOmitir.Should().Be(0);
        importacion.FilasEstimadasRechazadas.Should().Be(0);
        importacion.FilasEstimadasConAdvertencia.Should().Be(0);
        importacion.FilasCreadas.Should().Be(0);
        importacion.FilasActualizadas.Should().Be(0);
        importacion.FilasOmitidas.Should().Be(0);
        importacion.FilasRechazadas.Should().Be(0);
        importacion.FilasConAdvertencia.Should().Be(0);
    }

    // ── 2 ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RegistrarConteosPreview_EnEstadoPreviewGenerado_EscribeSoloEstimados()
    {
        var importacion = CrearImportacionEnPreview();

        importacion.RegistrarConteosPreview(
            filasACrear:         30,
            filasAActualizar:    50,
            filasAOmitir:        10,
            filasRechazadas:     10,
            filasConAdvertencia:  5);

        importacion.FilasEstimadasACrear.Should().Be(30);
        importacion.FilasEstimadasAActualizar.Should().Be(50);
        importacion.FilasEstimadasAOmitir.Should().Be(10);
        importacion.FilasEstimadasRechazadas.Should().Be(10);
        importacion.FilasEstimadasConAdvertencia.Should().Be(5);
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
    public void RegistrarConteosConfirmacion_EnEstadoPreviewGenerado_EscribeSoloConfirmados()
    {
        var importacion = CrearImportacionEnPreview();

        importacion.RegistrarConteosConfirmacion(
            filasCreadas:        20,
            filasActualizadas:   70,
            filasOmitidas:        5,
            filasRechazadas:      5,
            filasConAdvertencia:  3);

        importacion.FilasCreadas.Should().Be(20);
        importacion.FilasActualizadas.Should().Be(70);
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

    // ── 11 ── NUEVO ────────────────────────────────────────────────────────
    // Post-preview: los 5 campos de confirmación permanecen en cero.

    [Fact]
    public void RegistrarConteosPreview_NoModificaContadoresDeConfirmacion()
    {
        var importacion = CrearImportacionEnPreview();

        importacion.RegistrarConteosPreview(
            filasACrear:         40,
            filasAActualizar:    30,
            filasAOmitir:        10,
            filasRechazadas:     15,
            filasConAdvertencia:  5);

        importacion.FilasCreadas.Should().Be(0);
        importacion.FilasActualizadas.Should().Be(0);
        importacion.FilasOmitidas.Should().Be(0);
        importacion.FilasRechazadas.Should().Be(0);
        importacion.FilasConAdvertencia.Should().Be(0);
    }

    // ── 12 ── NUEVO ────────────────────────────────────────────────────────
    // Post-confirmación: los 5 estimados conservan EXACTAMENTE sus valores de preview.

    [Fact]
    public void RegistrarConteosConfirmacion_ConservaEstimadosIntactos()
    {
        var importacion = CrearImportacionEnPreview();

        importacion.RegistrarConteosPreview(
            filasACrear:         40,
            filasAActualizar:    30,
            filasAOmitir:        10,
            filasRechazadas:     15,
            filasConAdvertencia:  5);

        importacion.RegistrarConteosConfirmacion(
            filasCreadas:        38,
            filasActualizadas:   32,
            filasOmitidas:        9,
            filasRechazadas:     16,
            filasConAdvertencia:  6);

        // Los estimados no deben haber cambiado
        importacion.FilasEstimadasACrear.Should().Be(40);
        importacion.FilasEstimadasAActualizar.Should().Be(30);
        importacion.FilasEstimadasAOmitir.Should().Be(10);
        importacion.FilasEstimadasRechazadas.Should().Be(15);
        importacion.FilasEstimadasConAdvertencia.Should().Be(5);
    }

    // ── 13 ── NUEVO ────────────────────────────────────────────────────────
    // Divergencia TOCTOU: preview con conteos X, confirmación con conteos Y≠X.
    // Ambos juegos deben persistir íntegros y distintos.

    [Fact]
    public void RegistrarAmbosConteos_DivergenciaTOCTOU_AmbosPersistenDistintos()
    {
        var importacion = CrearImportacionEnPreview();

        importacion.RegistrarConteosPreview(
            filasACrear:         100,
            filasAActualizar:    200,
            filasAOmitir:         50,
            filasRechazadas:      30,
            filasConAdvertencia:  20);

        importacion.RegistrarConteosConfirmacion(
            filasCreadas:         95,
            filasActualizadas:   210,
            filasOmitidas:        55,
            filasRechazadas:      25,
            filasConAdvertencia:  15);

        // Estimados intactos
        importacion.FilasEstimadasACrear.Should().Be(100);
        importacion.FilasEstimadasAActualizar.Should().Be(200);
        importacion.FilasEstimadasAOmitir.Should().Be(50);
        importacion.FilasEstimadasRechazadas.Should().Be(30);
        importacion.FilasEstimadasConAdvertencia.Should().Be(20);

        // Confirmados correctos
        importacion.FilasCreadas.Should().Be(95);
        importacion.FilasActualizadas.Should().Be(210);
        importacion.FilasOmitidas.Should().Be(55);
        importacion.FilasRechazadas.Should().Be(25);
        importacion.FilasConAdvertencia.Should().Be(15);

        // Cada par es efectivamente distinto
        importacion.FilasEstimadasACrear.Should().NotBe(importacion.FilasCreadas);
        importacion.FilasEstimadasAActualizar.Should().NotBe(importacion.FilasActualizadas);
        importacion.FilasEstimadasAOmitir.Should().NotBe(importacion.FilasOmitidas);
        importacion.FilasEstimadasRechazadas.Should().NotBe(importacion.FilasRechazadas);
        importacion.FilasEstimadasConAdvertencia.Should().NotBe(importacion.FilasConAdvertencia);
    }
}
