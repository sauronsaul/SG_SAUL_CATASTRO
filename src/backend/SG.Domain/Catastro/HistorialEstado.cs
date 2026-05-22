using SG.Domain.Catastro.Enums;

namespace SG.Domain.Catastro;

/// <summary>
/// Registro INMUTABLE de cada transición de estado del predio.
/// No tiene updated_at, is_deleted ni método de modificación. Ver ADR 0030.
/// </summary>
public sealed class HistorialEstado
{
    public Guid Id { get; private set; }
    public Guid PredioId { get; private set; }
    public EstadoPredio EstadoAnterior { get; private set; }
    public EstadoPredio EstadoNuevo { get; private set; }
    public Guid CambiadoPor { get; private set; }
    public DateTime CambiadoAt { get; private set; }
    public string? Observaciones { get; private set; }

    private HistorialEstado() { }

    internal static HistorialEstado Registrar(
        Guid predioId,
        EstadoPredio estadoAnterior,
        EstadoPredio estadoNuevo,
        Guid cambiadoPor,
        string? observaciones = null)
    {
        return new HistorialEstado
        {
            Id = Guid.NewGuid(),
            PredioId = predioId,
            EstadoAnterior = estadoAnterior,
            EstadoNuevo = estadoNuevo,
            CambiadoPor = cambiadoPor,
            CambiadoAt = DateTime.UtcNow,
            Observaciones = observaciones?.Trim(),
        };
    }
}
