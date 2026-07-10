using System.Threading.Channels;

namespace SG.Application.Importacion.Versiones;

public interface IColaCargaVersionada
{
    ValueTask EncolarAsync(Guid datasetVersionId, CancellationToken ct = default);
    IAsyncEnumerable<Guid> LeerAsync(CancellationToken ct = default);
}

public sealed class ColaCargaVersionada : IColaCargaVersionada
{
    private readonly Channel<Guid> _canal = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EncolarAsync(Guid datasetVersionId, CancellationToken ct = default) =>
        _canal.Writer.WriteAsync(datasetVersionId, ct);

    public IAsyncEnumerable<Guid> LeerAsync(CancellationToken ct = default) =>
        _canal.Reader.ReadAllAsync(ct);
}
