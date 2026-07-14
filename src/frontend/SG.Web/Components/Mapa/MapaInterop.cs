using Microsoft.JSInterop;
using SG.Web.Models;

namespace SG.Web.Components.Mapa;

public sealed class MapaInterop(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private IJSObjectReference? _modulo;

    public async Task CrearAsync(
        string contenedorId,
        IReadOnlyList<double> limites,
        CamaraMapa? camara,
        string token,
        object capas,
        DotNetObjectReference<MapaCatastral> referenciaDotNet)
    {
        _modulo ??= await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/mapa.js");
        await _modulo.InvokeVoidAsync(
            "crearMapa",
            contenedorId,
            limites,
            camara,
            token,
            capas,
            referenciaDotNet);
    }

    public async Task CambiarVisibilidadAsync(string contenedorId, string capa, bool visible)
    {
        if (_modulo is not null)
            await _modulo.InvokeVoidAsync("cambiarVisibilidad", contenedorId, capa, visible);
    }

    public async Task<CamaraMapa?> ObtenerCamaraAsync(string contenedorId) =>
        _modulo is null
            ? null
            : await _modulo.InvokeAsync<CamaraMapa?>("obtenerCamara", contenedorId);

    public async Task DestruirAsync(string contenedorId)
    {
        if (_modulo is not null)
            await _modulo.InvokeVoidAsync("destruirMapa", contenedorId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_modulo is not null)
            await _modulo.DisposeAsync();
    }
}
