using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SG.Application.Catastro.Predios.AsignarCodigoOficial;
using SG.Application.Catastro.Predios.AsignarGeometria;
using SG.Application.Catastro.Predios.AsignarPropietario;
using SG.Application.Catastro.Predios.BuscarPorTriplete;
using SG.Application.Catastro.Predios.CambioEstado;
using SG.Application.Catastro.Predios.EliminarDocumento;
using SG.Application.Catastro.Predios.Listar;
using SG.Application.Catastro.Predios.ObtenerPorId;
using SG.Application.Catastro.Predios.RegistrarPredio;
using SG.Application.Catastro.Predios.SubirDocumento;
using SG.Application.Catastro.Predios.VerHistorial;
using SG.Domain.Catastro.Enums;
using SG.Domain.Common;

namespace SG.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/predios")]
public sealed class PrediosController(ISender sender) : ControllerBase
{
    // ──────────────────────────────── CRUD ────────────────────────────────

    public sealed record RegistrarPredioRequest(
        string UbicacionZona, string UbicacionManzana, string UbicacionLote,
        string? UbicacionBarrio, string? UbicacionDireccion, string? UbicacionReferencia,
        decimal SuperficieDeclarada, Guid UsoSueloId);

    [HttpPost]
    [Authorize(Roles = "Admin,Tecnico,Operador")]
    public async Task<IActionResult> Registrar(
        [FromBody] RegistrarPredioRequest body,
        CancellationToken ct)
    {
        var command = new RegistrarPredioCommand(
            body.UbicacionZona, body.UbicacionManzana, body.UbicacionLote,
            body.UbicacionBarrio, body.UbicacionDireccion, body.UbicacionReferencia,
            body.SuperficieDeclarada, body.UsoSueloId);
        var result = await sender.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(ObtenerPorId), new { id = result.Value }, new { id = result.Value })
            : MapError(result.Error);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> ObtenerPorId(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ObtenerPredioPorIdQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await sender.Send(new ListarPrediosQuery(page, pageSize), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    [HttpGet("{municipio}/buscar")]
    [Authorize(Roles = "Admin,Tecnico")]
    public async Task<IActionResult> Buscar(
        string municipio,
        [FromQuery] int distrito,
        [FromQuery] int manzana,
        [FromQuery] int predio,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new BuscarFichaPredioQuery(municipio, distrito, manzana, predio),
            ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    // ──────────────────────────── GEOMETRÍA ───────────────────────────────

    [HttpPut("{id:guid}/geometria")]
    [Authorize(Roles = "Admin,Tecnico")]
    public async Task<IActionResult> AsignarGeometria(
        Guid id,
        [FromBody] AsignarGeometriaRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new AsignarGeometriaPredioCommand(id, body.Geometria, body.Formato, body.Srid), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    // ──────────────────────────── PROPIETARIO ─────────────────────────────

    [HttpPut("{id:guid}/propietario")]
    [Authorize(Roles = "Admin,Tecnico,Operador")]
    public async Task<IActionResult> AsignarPropietario(
        Guid id,
        [FromBody] AsignarPropietarioRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(
            new AsignarPropietarioPredioCommand(id, body.PropietarioId, body.TipoDerecho, body.Porcentaje, body.VigenteDesde), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    // ──────────────────────────── CÓDIGO OFICIAL ──────────────────────────

    [HttpPut("{id:guid}/codigo-oficial")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AsignarCodigoOficial(
        Guid id,
        [FromBody] AsignarCodigoOficialRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(new AsignarCodigoOficialCommand(id, body.CodigoOficial), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    // ──────────────────────────── ESTADO ──────────────────────────────────

    [HttpPost("{id:guid}/estado/enviar-revision")]
    [Authorize(Roles = "Admin,Tecnico,Operador")]
    public async Task<IActionResult> EnviarARevision(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new EnviarARevisionCommand(id), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    [HttpPost("{id:guid}/estado/validar")]
    [Authorize(Roles = "Admin,Tecnico")]
    public async Task<IActionResult> Validar(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new ValidarPredioCommand(id), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    [HttpPost("{id:guid}/estado/observar")]
    [Authorize(Roles = "Admin,Tecnico")]
    public async Task<IActionResult> Observar(
        Guid id,
        [FromBody] ObservarRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(new ObservarPredioCommand(id, body.Observaciones), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    [HttpPost("{id:guid}/estado/retornar-borrador")]
    [Authorize(Roles = "Admin,Tecnico")]
    public async Task<IActionResult> RetornarBorrador(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new RetornarBorradorCommand(id), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    // ──────────────────────────── HISTORIAL ───────────────────────────────

    [HttpGet("{id:guid}/historial")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> VerHistorial(Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new VerHistorialEstadosQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error);
    }

    // ──────────────────────────── DOCUMENTOS ──────────────────────────────

    [HttpPost("{id:guid}/documentos")]
    [Authorize(Roles = "Admin,Tecnico,Operador")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> SubirDocumento(
        Guid id,
        [FromForm] IFormFile archivo,
        [FromForm] TipoDocumento tipoDocumento,
        CancellationToken ct)
    {
        await using var stream = archivo.OpenReadStream();
        var result = await sender.Send(
            new SubirDocumentoPredioCommand(id, archivo.FileName, archivo.ContentType, archivo.Length, stream, tipoDocumento),
            ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(ObtenerPorId), new { id }, new { documentoId = result.Value })
            : MapError(result.Error);
    }

    [HttpDelete("{id:guid}/documentos/{documentoId:guid}")]
    [Authorize(Roles = "Admin,Tecnico")]
    public async Task<IActionResult> EliminarDocumento(
        Guid id,
        Guid documentoId,
        [FromBody] EliminarDocumentoRequest body,
        CancellationToken ct)
    {
        var result = await sender.Send(new EliminarDocumentoPredioCommand(id, documentoId, body.Motivo), ct);
        return result.IsSuccess ? NoContent() : MapError(result.Error);
    }

    // ──────────────────────────── ERROR MAPPING ───────────────────────────

    private ObjectResult MapError(DomainError error) => error.Code switch
    {
        _ when error.Code.EndsWith(".NoEncontrado", StringComparison.Ordinal) =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status404NotFound),
        "Predio.CodigoCatastralDuplicado" or
        "Predio.TripleteCatastralDuplicado" or
        "Relacion.PropietarioYaVigente" or
        "Documento.YaEliminado" =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status409Conflict),
        _ when error.Code.StartsWith("Predio.TransicionInvalida", StringComparison.Ordinal) =>
            Problem(detail: error.Message, statusCode: StatusCodes.Status422UnprocessableEntity),
        _ => Problem(detail: error.Message, statusCode: StatusCodes.Status400BadRequest),
    };

    // ──────────────────────────── REQUEST TYPES ───────────────────────────

    public sealed record AsignarGeometriaRequest(string Geometria, string Formato, int? Srid);

    public sealed record AsignarPropietarioRequest(
        Guid PropietarioId,
        TipoDerecho TipoDerecho,
        decimal Porcentaje,
        DateOnly VigenteDesde);

    public sealed record AsignarCodigoOficialRequest(string CodigoOficial);

    public sealed record ObservarRequest(string Observaciones);

    public sealed record EliminarDocumentoRequest(string Motivo);
}
