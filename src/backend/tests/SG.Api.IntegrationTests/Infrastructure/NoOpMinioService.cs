using SG.Application.Abstractions;
using System.Collections.Concurrent;

namespace SG.Api.IntegrationTests.Infrastructure;

internal sealed class NoOpMinioService : IMinioService
{
    private static readonly ConcurrentDictionary<string, byte[]> Objetos = new();

    public Task InicializarAsync(CancellationToken ct = default) => Task.CompletedTask;

    public async Task<string> SubirAsync(
        Stream stream, string key, string contentType, long sizeBytes, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        Objetos[key] = buffer.ToArray();
        return key;
    }

    public Task<Stream> DescargarAsync(string key, CancellationToken ct = default)
    {
        if (!Objetos.TryGetValue(key, out var contenido))
            throw new InvalidOperationException($"Objeto de prueba no encontrado: {key}.");

        return Task.FromResult<Stream>(new MemoryStream(contenido, writable: false));
    }

    public Task MoverAPapeleraAsync(string key, CancellationToken ct = default)
    {
        Objetos.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}
