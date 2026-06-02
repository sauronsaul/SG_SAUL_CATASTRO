using SG.Domain.Common;

namespace SG.Domain.Importacion;

public sealed class MapeoColumna : Entity
{
    private readonly List<EquivalenciaValor> _equivalencias = [];

    public Guid PerfilImportacionId { get; private set; }
    public string NombreColumnaOrigen { get; private set; } = string.Empty;
    public string CampoDestino { get; private set; } = string.Empty;
    public bool EsObligatorio { get; private set; }

    public IReadOnlyCollection<EquivalenciaValor> Equivalencias => _equivalencias.AsReadOnly();

    private MapeoColumna() { }

    internal static MapeoColumna Crear(
        Guid perfilId,
        string nombreColumnaOrigen,
        string campoDestino,
        bool esObligatorio) =>
        new()
        {
            PerfilImportacionId = perfilId,
            NombreColumnaOrigen = nombreColumnaOrigen.Trim(),
            CampoDestino = campoDestino.Trim(),
            EsObligatorio = esObligatorio,
        };

    internal EquivalenciaValor AgregarEquivalencia(string valorOrigen, string valorDestino)
    {
        var eq = EquivalenciaValor.Crear(Id, valorOrigen, valorDestino);
        _equivalencias.Add(eq);
        return eq;
    }
}
