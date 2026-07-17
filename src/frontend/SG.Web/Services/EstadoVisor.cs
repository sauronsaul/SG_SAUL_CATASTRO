using SG.Web.Models;

namespace SG.Web.Services;

public sealed class EstadoVisor
{
    private readonly Dictionary<string, bool> _visibilidad = new(StringComparer.Ordinal);

    public CamaraMapa? Camara { get; private set; }

    public bool EsVisible(string nombre) =>
        _visibilidad.TryGetValue(nombre, out var visible) && visible;

    public bool CambiarVisibilidad(string nombre, bool visible)
    {
        if (!_visibilidad.ContainsKey(nombre))
            return false;

        _visibilidad[nombre] = visible;
        return true;
    }

    public void ActualizarCamara(CamaraMapa camara) => Camara = camara;

    public void ConfigurarMunicipio(IEnumerable<string> capas)
    {
        _visibilidad.Clear();
        foreach (var capa in capas)
            _visibilidad[capa] = true;
        Camara = null;
    }
}
