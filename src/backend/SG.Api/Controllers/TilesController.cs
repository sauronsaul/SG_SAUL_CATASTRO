using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SG.Application.GIS.Tiles;

namespace SG.Api.Controllers;

[ApiController]
[Route("api/tiles")]
[Authorize(Roles = "Admin,Tecnico")]
public sealed class TilesController(ISender sender) : ControllerBase
{
    private const string ContentTypeMvt = "application/vnd.mapbox-vector-tile";

    [HttpGet("{capa}/{z:int}/{x:int}/{y:int}.mvt")]
    public async Task<IActionResult> Obtener(
        string capa,
        int z,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        if (!CatalogoCapasTile.IntentarResolver(capa, out var capaTile))
            return NotFound();

        if (!CoordenadasTile.SonValidas(z, x, y))
            return BadRequest(new { error = "Coordenadas de tile fuera de rango." });

        var tile = await sender.Send(
            new ObtenerTileVectorialQuery(capaTile, z, x, y),
            cancellationToken);
        if (tile is null)
            return NoContent();

        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.Vary = "Authorization";
        Response.Headers.ETag = tile.ETag;

        if (Request.Headers.IfNoneMatch.Any(valor =>
                string.Equals(valor, tile.ETag, StringComparison.Ordinal) || valor == "*"))
            return StatusCode(StatusCodes.Status304NotModified);

        return tile.Contenido.Length == 0
            ? NoContent()
            : File(tile.Contenido, ContentTypeMvt);
    }
}
