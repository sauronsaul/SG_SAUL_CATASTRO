using FluentAssertions;
using SG.Application.Abstractions;
using SG.Application.Importacion.GenerarPreview;
using SG.Contracts.Importacion;
using SG.Domain.Catastro.Enums;

namespace SG.Application.Tests.Importacion;

public sealed class ClasificadorAccionPreviewTests
{
    // Diccionario vacío: ningún predio existe en la BD.
    private static readonly IReadOnlyDictionary<(string, string, string), EstadoPredio> SinExistentes =
        new Dictionary<(string, string, string), EstadoPredio>();

    // Tripleta estándar usada por los helpers (coincide con lo que mapea PerfilPrediosSimple).
    private const string ZonaRef    = "4";
    private const string ManzanaRef = "12";
    private const string LoteRef    = "7";

    // Fila mapeada con tripleta completa y clasificación Ok.
    private static ResultadoMapeoFila FilaOk(
        string zona    = ZonaRef,
        string manzana = ManzanaRef,
        string lote    = LoteRef) =>
        new(NumeroFila: 1,
            Clasificacion: ClasificacionFila.Ok,
            ValoresMapeados: new Dictionary<string, string?>
            {
                ["UbicacionCatastral.Zona"]    = zona,
                ["UbicacionCatastral.Manzana"] = manzana,
                ["UbicacionCatastral.Lote"]    = lote,
            },
            Geometria: null,
            Advertencias: [],
            Errores: []);

    // Fila rechazada por el MapeadorImportacion (sin tripleta, sin geometría, etc.).
    private static ResultadoMapeoFila FilaRechazada() =>
        new(NumeroFila: 2,
            Clasificacion: ClasificacionFila.Rechazada,
            ValoresMapeados: new Dictionary<string, string?>(),
            Geometria: null,
            Advertencias: [],
            Errores: ["La fila no tiene geometría y no puede importarse."]);

    // ── predio inexistente ─────────────────────────────────────────────────

    [Fact]
    public void Clasificar_PredioNoExiste_DevuelveCrear()
    {
        ClasificadorAccionPreview.Clasificar(FilaOk(), SinExistentes)
            .Should().Be(AccionPreviewFila.Crear);
    }

    // ── estados mutables → Actualizar ─────────────────────────────────────

    [Fact]
    public void Clasificar_PredioEnImportado_DevuelveActualizar()
    {
        var existentes = ExistentesConEstado(EstadoPredio.Importado);

        ClasificadorAccionPreview.Clasificar(FilaOk(), existentes)
            .Should().Be(AccionPreviewFila.Actualizar);
    }

    [Fact]
    public void Clasificar_PredioEnBorrador_DevuelveActualizar()
    {
        var existentes = ExistentesConEstado(EstadoPredio.Borrador);

        ClasificadorAccionPreview.Clasificar(FilaOk(), existentes)
            .Should().Be(AccionPreviewFila.Actualizar);
    }

    // ── estados protegidos → Omitir ────────────────────────────────────────

    [Fact]
    public void Clasificar_PredioEnValidado_DevuelveOmitir()
    {
        var existentes = ExistentesConEstado(EstadoPredio.Validado);

        ClasificadorAccionPreview.Clasificar(FilaOk(), existentes)
            .Should().Be(AccionPreviewFila.Omitir);
    }

    [Fact]
    public void Clasificar_PredioEnRevision_DevuelveOmitir()
    {
        var existentes = ExistentesConEstado(EstadoPredio.EnRevision);

        ClasificadorAccionPreview.Clasificar(FilaOk(), existentes)
            .Should().Be(AccionPreviewFila.Omitir);
    }

    [Fact]
    public void Clasificar_PredioEnObservado_DevuelveOmitir()
    {
        var existentes = ExistentesConEstado(EstadoPredio.Observado);

        ClasificadorAccionPreview.Clasificar(FilaOk(), existentes)
            .Should().Be(AccionPreviewFila.Omitir);
    }

    // ── fila rechazada por el mapeador ─────────────────────────────────────

    [Fact]
    public void Clasificar_FilaRechazadaPorMapeador_DevuelveRechazada()
    {
        // Aunque el predio exista, una fila rechazada por el mapeador no se importa.
        var existentes = ExistentesConEstado(EstadoPredio.Borrador);

        ClasificadorAccionPreview.Clasificar(FilaRechazada(), existentes)
            .Should().Be(AccionPreviewFila.Rechazada);
    }

    // ── helper ────────────────────────────────────────────────────────────

    private static Dictionary<(string, string, string), EstadoPredio>
        ExistentesConEstado(EstadoPredio estado) =>
        new()
        {
            [(ZonaRef, ManzanaRef, LoteRef)] = estado,
        };
}
