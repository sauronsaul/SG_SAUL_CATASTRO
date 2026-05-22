using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SG.Application.Catastro.Propietarios.Listar;
using SG.Application.Catastro.Propietarios.ObtenerPorId;
using SG.Application.Catastro.Propietarios.RegistrarPersonaJuridica;
using SG.Application.Catastro.Propietarios.RegistrarPersonaNatural;
using SG.Domain.Common;

namespace SG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/propietarios")]
public sealed class PropietariosController(ISender sender) : ControllerBase
{
    public sealed record PersonaNaturalRequest(
        string Nombre, string Apellidos, string Cedula,
        string? Email, string? Telefono, string? Direccion);

    public sealed record PersonaJuridicaRequest(
        string RazonSocial, string Nit, string? RepresentanteLegal,
        string? Email, string? Telefono, string? Direccion);

    [HttpPost("persona-natural")]
    [Authorize(Roles = "Admin,Tecnico,Operador")]
    public async Task<IActionResult> RegistrarPersonaNatural(
        [FromBody] PersonaNaturalRequest body,
        CancellationToken ct)
    {
        var command = new RegistrarPersonaNaturalCommand(
            body.Nombre, body.Apellidos, body.Cedula,
            body.Email, body.Telefono, body.Direccion);
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(ObtenerPorId), new { id = result.Value }, new { id = result.Value })
            : MapError(result.Error);
    }

    [HttpPost("persona-juridica")]
    [Authorize(Roles = "Admin,Tecnico,Operador")]
    public async Task<IActionResult> RegistrarPersonaJuridica(
        [FromBody] PersonaJuridicaRequest body,
        CancellationToken ct)
    {
        var command = new RegistrarPersonaJuridicaCommand(
            body.RazonSocial, body.Nit, body.RepresentanteLegal,
            body.Email, body.Telefono, body.Direccion);
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(ObtenerPorId), new { id = result.Value }, new { id = result.Value })
            : MapError(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObtenerPorId(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ObtenerPropietarioPorIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? busqueda = null,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListarPropietariosQuery(page, pageSize, busqueda), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    private ObjectResult MapError(DomainError error) => error.Code switch
    {
        _ when error.Code.EndsWith(".NoEncontrado", StringComparison.Ordinal) =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status404NotFound),
        "Propietario.CedulaDuplicada" or "Propietario.NitDuplicado" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status409Conflict),
        _ => Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
    };
}
