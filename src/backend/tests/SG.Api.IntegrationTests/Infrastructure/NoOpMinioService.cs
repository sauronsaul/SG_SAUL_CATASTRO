using SG.Application.Abstractions;

namespace SG.Api.IntegrationTests.Infrastructure;

internal sealed class NoOpMinioService : IMinioService
{
    public Task InicializarAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<string> SubirAsync(
        Stream stream, string key, string contentType, long sizeBytes, CancellationToken ct = default)
        => Task.FromResult(key);

    public Task<Stream> DescargarAsync(string key, CancellationToken ct = default)
        => Task.FromResult<Stream>(Stream.Null);

    public Task MoverAPapeleraAsync(string key, CancellationToken ct = default)
        => Task.CompletedTask;
}
