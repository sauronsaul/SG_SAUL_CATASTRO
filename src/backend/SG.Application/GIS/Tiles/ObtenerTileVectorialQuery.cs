using MediatR;
using SG.Application.Abstractions.Catalogos;
using SG.Application.Abstractions.GIS;
using SG.Application.Abstractions.Importacion;
using SG.Domain.Catalogos;
using SG.Domain.Importacion;

namespace SG.Application.GIS.Tiles;

public sealed record ObtenerTileVectorialQuery(
    string MunicipioCodigo,
    TipoCapa Capa,
    string NombreCapa,
    int Z,
    int X,
    int Y) : IRequest<ResultadoSolicitudTile>;

public enum EstadoSolicitudTile
{
    Disponible,
    MunicipioInvalido,
    MunicipioNoEncontrado,
    CapaNoEncontrada,
    SinDatasetActivo,
}

public sealed record ResultadoTileVectorial(byte[] Contenido, string ETag);

public sealed record ResultadoSolicitudTile(
    EstadoSolicitudTile Estado,
    ResultadoTileVectorial? Tile = null);

internal sealed class ObtenerTileVectorialQueryHandler(
    ITileVectorialService service,
    IMunicipioRepositorio municipios,
    IEsquemaCapasMunicipioRepositorio esquemas)
    : IRequestHandler<ObtenerTileVectorialQuery, ResultadoSolicitudTile>
{
    public async Task<ResultadoSolicitudTile> Handle(
        ObtenerTileVectorialQuery request,
        CancellationToken cancellationToken)
    {
        if (!Municipio.EsCodigoIneValido(request.MunicipioCodigo))
            return new(EstadoSolicitudTile.MunicipioInvalido);

        if (!await municipios.ExistePorCodigoIneAsync(request.MunicipioCodigo, cancellationToken))
            return new(EstadoSolicitudTile.MunicipioNoEncontrado);

        var esquema = await esquemas.ListarAsync(request.MunicipioCodigo, cancellationToken);
        if (!esquema.Any(x => x.TipoCapa == request.Capa))
            return new(EstadoSolicitudTile.CapaNoEncontrada);

        var persistido = await service.ObtenerAsync(
            request.MunicipioCodigo,
            request.Capa,
            request.NombreCapa,
            request.Z,
            request.X,
            request.Y,
            cancellationToken);

        return persistido is null
            ? new(EstadoSolicitudTile.SinDatasetActivo)
            : new(
                EstadoSolicitudTile.Disponible,
                new ResultadoTileVectorial(
                    persistido.Contenido,
                ETagTile.Calcular(
                    request.MunicipioCodigo,
                    persistido.DatasetVersionId,
                    persistido.NumeroVersion,
                    request.Capa,
                    request.Z,
                    request.X,
                    request.Y)));
    }
}
