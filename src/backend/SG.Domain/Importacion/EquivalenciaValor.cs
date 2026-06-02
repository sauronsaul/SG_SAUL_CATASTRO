using SG.Domain.Common;

namespace SG.Domain.Importacion;

public sealed class EquivalenciaValor : Entity
{
    public Guid MapeoColumnaId { get; private set; }
    public string ValorOrigen { get; private set; } = string.Empty;
    public string ValorDestino { get; private set; } = string.Empty;

    private EquivalenciaValor() { }

    internal static EquivalenciaValor Crear(Guid mapeoColumnaId, string valorOrigen, string valorDestino) =>
        new()
        {
            MapeoColumnaId = mapeoColumnaId,
            ValorOrigen = valorOrigen.Trim(),
            ValorDestino = valorDestino.Trim(),
        };
}
