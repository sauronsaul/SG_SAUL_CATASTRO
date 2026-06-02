using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Importacion;
using SG.Domain.Common;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Application.Importacion.ObtenerDetalle;

/// <summary>
/// Para importaciones en estado PreviewGenerado re-lee el shapefile desde MinIO y
/// devuelve TODAS las filas clasificadas (sin límite de muestra).
/// Para otros estados devuelve solo metadatos + conteos (Filas = null).
/// </summary>
public sealed class ObtenerDetalleImportacionHandler(
    IImportacionRepositorio importaciones,
    IPerfilImportacionRepositorio perfiles,
    IMinioService minio,
    PipelineShapefileService pipeline)
    : IRequestHandler<ObtenerDetalleImportacionQuery, Result<ImportacionDetalleDto>>
{
    public async Task<Result<ImportacionDetalleDto>> Handle(
        ObtenerDetalleImportacionQuery request,
        CancellationToken cancellationToken)
    {
        var importacion = await importaciones.ObtenerPorIdSinTrackingAsync(
            request.ImportacionId, cancellationToken);

        if (importacion is null)
            return Result.Failure<ImportacionDetalleDto>(ImportacionDomain.ImportacionErrores.NoEncontrada);

        // Para estados distintos de PreviewGenerado no hay filas que exponer:
        // las filas ya fueron procesadas (Confirmada) o el proceso falló (Fallida).
        if (importacion.Estado != ImportacionDomain.EstadoImportacion.PreviewGenerado)
            return Result.Success(ToDetalle(importacion, filas: null));

        var perfil = await perfiles.ObtenerPorIdAsync(importacion.PerfilId, cancellationToken);
        if (perfil is null)
            return Result.Failure<ImportacionDetalleDto>(ImportacionDomain.PerfilImportacionErrores.NoEncontrado);

        await using var zipStream = await minio.DescargarAsync(importacion.RutaMinioZip, cancellationToken);
        using var zipBuffer = new MemoryStream();
        await zipStream.CopyToAsync(zipBuffer, cancellationToken);
        zipBuffer.Position = 0;

        var filas = await pipeline.ProcesarAsync(zipBuffer, perfil, cancellationToken);

        return Result.Success(ToDetalle(importacion, filas));
    }

    private static ImportacionDetalleDto ToDetalle(
        ImportacionDomain.Importacion i,
        IReadOnlyList<FilaPreviewDto>? filas) =>
        new(i.Id,
            i.NombreArchivo,
            i.Estado.ToString(),
            i.FechaImportacion,
            i.ImportadoPorId,
            i.PerfilId,
            i.TotalFilas,
            i.FilasImportadas,
            i.FilasConAdvertencia,
            i.FilasRechazadas,
            i.FilasOmitidas,
            filas);
}
