using MediatR;
using SG.Application.Abstractions.GIS;

namespace SG.Application.GIS.Tiles;

public sealed record ObtenerTileVectorialQuery(CapaTile Capa, int Z, int X, int Y)
    : IRequest<ResultadoTileVectorial?>;

public sealed record ResultadoTileVectorial(Guid DatasetVersionId, byte[] Contenido, string ETag);

internal sealed class ObtenerTileVectorialQueryHandler(ITileVectorialService service)
    : IRequestHandler<ObtenerTileVectorialQuery, ResultadoTileVectorial?>
{
    public async Task<ResultadoTileVectorial?> Handle(
        ObtenerTileVectorialQuery request,
        CancellationToken cancellationToken)
    {
        var persistido = await service.ObtenerAsync(
            request.Capa,
            request.Z,
            request.X,
            request.Y,
            cancellationToken);

        return persistido is null
            ? null
            : new ResultadoTileVectorial(
                persistido.DatasetVersionId,
                persistido.Contenido,
                ETagTile.Calcular(
                    persistido.DatasetVersionId,
                    request.Capa,
                    request.Z,
                    request.X,
                    request.Y));
    }
}
