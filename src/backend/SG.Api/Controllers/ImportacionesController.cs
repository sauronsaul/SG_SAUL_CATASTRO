using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SG.Application.Importacion.GenerarPreview;
using SG.Application.Importacion.Listar;
using SG.Application.Importacion.ObtenerDetalle;
using SG.Application.Importacion.Versiones;
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
    /// Recibe un ZIP con las siete capas SHP de una entrega y encola su carga versionada.
    /// El paquete se conserva en MinIO antes de devolver la respuesta Accepted.
    /// </summary>
    [HttpPost("versiones")]
    [Authorize(Roles = "Admin,Tecnico")]
    [RequestSizeLimit(110 * 1024 * 1024)]
    public async Task<IActionResult> CrearVersion(
        [FromForm(Name = "municipio_codigo")] string? municipioCodigo,
        [FromForm] IFormFile? paquete,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(municipioCodigo))
            return Problem(
                detail: "Se requiere el campo 'municipio_codigo'.",
                statusCode: StatusCodes.Status400BadRequest);

        // La ausencia del archivo es un error de request: no debe crear una versión Fallida.
        if (paquete is null)
            return Problem(
                detail: "Se requiere el archivo 'paquete'.",
                statusCode: StatusCodes.Status400BadRequest);

        await using var stream = paquete.OpenReadStream();
        var result = await sender.Send(new CrearVersionImportacionCommand(
            municipioCodigo,
            paquete.FileName,
            stream,
            paquete.Length), ct);

        return result.IsSuccess
            ? AcceptedAtAction(nameof(ObtenerEstadoVersion), new { id = result.Value.DatasetVersionId }, result.Value)
            : MapError(result.Error);
    }

    /// <summary>Estado, progreso y error persistido de una carga versionada.</summary>
    [HttpGet("versiones/{id:guid}")]
    [Authorize(Roles = "Admin,Tecnico")]
    public async Task<IActionResult> ObtenerEstadoVersion(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ObtenerEstadoVersionImportacionQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    /// <summary>
    /// Genera un preview de la importación: clasifica cada fila del shapefile como
    /// Crear / Actualizar / Omitir / Rechazada sin escribir en dominio.predios.
    /// </summary>
    [HttpPost("versiones/{id:guid}/activar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ActivarVersion(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ActivarVersionImportacionCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    /// <summary>
    /// Descarta una versión en PreviewListo y purga sus filas de capas importadas.
    /// El paquete fuente permanece conservado en MinIO.
    /// </summary>
    [HttpPost("versiones/{id:guid}/descartar")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DescartarVersion(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new DescartarVersionImportacionCommand(id), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

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
    /// Endpoint legado fuera de servicio. El handler se conserva temporalmente,
    /// pero la reconciliación versionada es el único escritor automatizado del maestro.
    /// </summary>
    [HttpPost("{id:guid}/confirmar")]
    [Authorize(Roles = "Admin,Tecnico")]
    public IActionResult Confirmar(Guid id) =>
        Problem(
            detail: "Este flujo fue retirado. Use POST /api/importaciones/versiones y luego /api/importaciones/versiones/{id}/activar.",
            statusCode: StatusCodes.Status410Gone);

    private ObjectResult MapError(DomainError error) => error.Code switch
    {
        "PerfilImportacion.NoEncontrado" or "Importacion.NoEncontrada" or "VersionImportacion.NoEncontrada" or
        "VersionImportacion.MunicipioNoEncontrado" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status404NotFound),
        "Importacion.YaConfirmada" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status409Conflict),
        "Importacion.EstadoInvalidoParaConfirmar" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status422UnprocessableEntity),
        "VersionImportacion.EstadoNoActivable" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status409Conflict),
        "VersionImportacion.EstadoNoDescartable" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status409Conflict),
        "VersionImportacion.ReporteNoDisponible" or
        "VersionImportacion.ReporteConBloqueantes" or
        "VersionImportacion.ReconciliacionInvalida" or
        "VersionImportacion.EsquemaMunicipalNoConfigurado" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status422UnprocessableEntity),
        "VersionImportacion.UsuarioNoDisponible" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status401Unauthorized),
        _ => Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
    };
}
