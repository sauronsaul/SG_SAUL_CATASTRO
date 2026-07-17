using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SG.Application.GIS.Tiles;
using SG.Application.GIS.Visor;

namespace SG.Api.Controllers;

[ApiController]
[Route("api/tiles")]
[Authorize(Roles = "Admin,Tecnico")]
public sealed class TilesController(ISender sender) : ControllerBase
{
    private const string ContentTypeMvt = "application/vnd.mapbox-vector-tile";

    [HttpGet("{municipio}/{capa}/{z:int}/{x:int}/{y:int}.mvt")]
    public async Task<IActionResult> Obtener(
        string municipio,
        string capa,
        int z,
        int x,
        int y,
        CancellationToken cancellationToken)
    {
        if (!CatalogoPresentacionCapasVisor.IntentarResolver(capa, out var tipoCapa))
            return NotFound();

        if (!CoordenadasTile.SonValidas(z, x, y))
            return BadRequest(new { error = "Coordenadas de tile fuera de rango." });

        var tile = await sender.Send(
            new ObtenerTileVectorialQuery(municipio, tipoCapa, capa, z, x, y),
            cancellationToken);
        if (tile.Estado == EstadoSolicitudTile.MunicipioInvalido)
            return BadRequest(new { error = "El código INE del municipio es inválido." });
        if (tile.Estado is EstadoSolicitudTile.MunicipioNoEncontrado or EstadoSolicitudTile.CapaNoEncontrada)
            return NotFound();
        if (tile.Estado == EstadoSolicitudTile.SinDatasetActivo)
            return Conflict(new { error = "El municipio no tiene un dataset activo." });

        Response.Headers.CacheControl = "private, no-cache";
        Response.Headers.Vary = "Authorization";
        Response.Headers.ETag = tile.Tile!.ETag;

        if (Request.Headers.IfNoneMatch.Any(valor =>
                string.Equals(valor, tile.Tile.ETag, StringComparison.Ordinal) || valor == "*"))
            return StatusCode(StatusCodes.Status304NotModified);

        return tile.Tile.Contenido.Length == 0
            ? NoContent()
            : File(tile.Tile.Contenido, ContentTypeMvt);
    }
}
