using SG.Domain.Common;

namespace SG.Domain.Importacion;

public static class PerfilImportacionErrores
{
    public static readonly DomainError NoEncontrado =
        new("PerfilImportacion.NoEncontrado", "El perfil de importación no fue encontrado.");
}

public sealed class PerfilImportacion : AggregateRoot
{
    private readonly List<MapeoColumna> _mapeos = [];

    public string Nombre { get; private set; } = string.Empty;
    public string? Descripcion { get; private set; }
    public TipoCapa TipoCapa { get; private set; }
    public string NombreArchivoShp { get; private set; } = string.Empty;

    public IReadOnlyCollection<MapeoColumna> Mapeos => _mapeos.AsReadOnly();

    private PerfilImportacion() { }

    public static PerfilImportacion Crear(
        string nombre,
        TipoCapa tipoCapa,
        string nombreArchivoShp,
        string? descripcion = null) =>
        new()
        {
            Nombre = nombre.Trim(),
            TipoCapa = tipoCapa,
            NombreArchivoShp = nombreArchivoShp.Trim(),
            Descripcion = descripcion?.Trim(),
        };

    public MapeoColumna AgregarMapeo(
        string nombreColumnaOrigen,
        string campoDestino,
        bool esObligatorio)
    {
        var mapeo = MapeoColumna.Crear(Id, nombreColumnaOrigen, campoDestino, esObligatorio);
        _mapeos.Add(mapeo);
        return mapeo;
    }

    public EquivalenciaValor AgregarEquivalencia(
        Guid mapeoColumnaId,
        string valorOrigen,
        string valorDestino)
    {
        var mapeo = _mapeos.First(m => m.Id == mapeoColumnaId);
        return mapeo.AgregarEquivalencia(valorOrigen, valorDestino);
    }
}
