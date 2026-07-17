using SG.Application.GIS.Tiles;
using SG.Domain.Importacion;

namespace SG.Application.Abstractions.GIS;

public interface ITileVectorialService
{
    Task<TileVectorialPersistido?> ObtenerAsync(
        string municipioCodigo,
        TipoCapa capa,
        string nombreCapa,
        int z,
        int x,
        int y,
        CancellationToken cancellationToken);
}

public sealed record TileVectorialPersistido(
    Guid DatasetVersionId,
    int NumeroVersion,
    byte[] Contenido);
