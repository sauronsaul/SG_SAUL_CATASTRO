using SG.Application.GIS.Tiles;

namespace SG.Application.Abstractions.GIS;

public interface ITileVectorialService
{
    Task<TileVectorialPersistido?> ObtenerAsync(
        CapaTile capa,
        int z,
        int x,
        int y,
        CancellationToken cancellationToken);
}

public sealed record TileVectorialPersistido(Guid DatasetVersionId, byte[] Contenido);
