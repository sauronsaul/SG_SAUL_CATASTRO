using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SG.Application.GIS.Visor;
using SG.Domain.Common;

namespace SG.Api.Controllers;

[ApiController]
[Route("api/visor")]
[Authorize(Roles = "Admin,Tecnico")]
public sealed class VisorController(ISender sender) : ControllerBase
{
    [HttpGet("municipios")]
    public async Task<IActionResult> ListarMunicipios(CancellationToken ct) =>
        Ok(await sender.Send(new ListarMunicipiosVisorQuery(), ct));

    [HttpGet("{municipio}/configuracion")]
    public async Task<IActionResult> ObtenerConfiguracion(string municipio, CancellationToken ct)
    {
        var resultado = await sender.Send(new ObtenerConfiguracionVisorQuery(municipio), ct);
        return resultado.IsSuccess ? Ok(resultado.Value) : MapearError(resultado.Error);
    }

    private ObjectResult MapearError(DomainError error) => error.Code switch
    {
        "Visor.MunicipioCodigoInvalido" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
        "Visor.MunicipioNoEncontrado" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status404NotFound),
        "Visor.DatasetActivoNoDisponible" or "Visor.EsquemaNoConfigurado" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status409Conflict),
        "Visor.DatasetSinGeometrias" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status422UnprocessableEntity),
        _ => Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
    };
}
