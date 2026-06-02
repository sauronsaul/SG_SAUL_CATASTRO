using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SG.Application.Importacion.Confirmar;
using SG.Application.Importacion.GenerarPreview;
using SG.Application.Importacion.Listar;
using SG.Application.Importacion.ObtenerDetalle;
using SG.Domain.Common;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/importaciones")]
public sealed class ImportacionesController(ISender sender) : ControllerBase
{
    /// <summary>
    /// Listado paginado del historial de importaciones, con filtros opcionales.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin,Tecnico")]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? estado = null,
        [FromQuery] DateTime? fechaDesde = null,
        [FromQuery] DateTime? fechaHasta = null,
        CancellationToken ct = default)
    {
        ImportacionDomain.EstadoImportacion? estadoEnum = null;
        if (estado is not null)
        {
            if (!Enum.TryParse<ImportacionDomain.EstadoImportacion>(estado, ignoreCase: true, out var parsed))
                return Problem(detail: $"Estado '{estado}' no reconocido.", statusCode: StatusCodes.Status400BadRequest);
            estadoEnum = parsed;
        }

        var result = await sender.Send(
            new ListarImportacionesQuery(page, pageSize, estadoEnum, fechaDesde, fechaHasta), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    /// <summary>
    /// Detalle de una importación. Cuando el estado es PreviewGenerado devuelve
    /// la lista completa de filas clasificadas (re-lee el shapefile desde MinIO).
    /// Para otros estados devuelve sólo metadatos y conteos.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin,Tecnico")]
    public async Task<IActionResult> ObtenerDetalle(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ObtenerDetalleImportacionQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    /// <summary>
    /// Genera un preview de la importación: clasifica cada fila del shapefile como
    /// Crear / Actualizar / Omitir / Rechazada sin escribir en dominio.predios.
    /// </summary>
    [HttpPost("preview")]
    [Authorize(Roles = "Admin,Tecnico")]
    [RequestSizeLimit(110 * 1024 * 1024)] // 110 MB — margen sobre el límite de 100 MB del comando
    public async Task<IActionResult> GenerarPreview(
        [FromForm] IFormFile archivo,
        [FromForm] Guid perfilId,
        CancellationToken ct)
    {
        await using var stream = archivo.OpenReadStream();
        var command = new GenerarPreviewImportacionCommand(
            perfilId,
            archivo.FileName,
            stream,
            archivo.Length);

        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error);
    }

    /// <summary>
    /// Fase 2: confirma la importación — escribe predios/construcciones en el dominio.
    /// Idempotente: segunda llamada sobre una importación ya confirmada devuelve 409.
    /// </summary>
    [HttpPost("{id:guid}/confirmar")]
    [Authorize(Roles = "Admin,Tecnico")]
    public async Task<IActionResult> Confirmar(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ConfirmarImportacionCommand(id), ct);
        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error);
    }

    private ObjectResult MapError(DomainError error) => error.Code switch
    {
        "PerfilImportacion.NoEncontrado" or "Importacion.NoEncontrada" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status404NotFound),
        "Importacion.YaConfirmada" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status409Conflict),
        "Importacion.EstadoInvalidoParaConfirmar" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status422UnprocessableEntity),
        _ => Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
    };
}
