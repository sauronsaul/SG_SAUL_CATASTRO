using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SG.Application.Importacion.Perfiles;

namespace SG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/perfiles-importacion")]
public sealed class PerfilesImportacionController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Lista los perfiles de importación disponibles. Todos los roles autenticados
    /// pueden consultarlos (necesario para elegir perfil en el flujo de preview).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var result = await sender.Send(new ListarPerfilesQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : Problem(statusCode: StatusCodes.Status500InternalServerError);
    }
}
