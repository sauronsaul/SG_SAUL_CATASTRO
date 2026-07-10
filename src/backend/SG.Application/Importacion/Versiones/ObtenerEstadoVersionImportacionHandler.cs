using System.Text.Json;
using MediatR;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public sealed class ObtenerEstadoVersionImportacionHandler(IDatasetVersionRepositorio versiones)
    : IRequestHandler<ObtenerEstadoVersionImportacionQuery, Result<EstadoVersionImportacionDto>>
{
    public async Task<Result<EstadoVersionImportacionDto>> Handle(
        ObtenerEstadoVersionImportacionQuery request,
        CancellationToken cancellationToken)
    {
        var version = await versiones.ObtenerPorIdSinTrackingAsync(request.DatasetVersionId, cancellationToken);
        if (version is null)
            return Result.Failure<EstadoVersionImportacionDto>(VersionImportacionErrores.NoEncontrada);

        var reporte = JsonSerializer.Deserialize<ReportePreliminarVersionDto>(version.ReportePreliminar)
            ?? new ReportePreliminarVersionDto(null, new Dictionary<string, int>());

        return Result.Success(new EstadoVersionImportacionDto(
            version.Id,
            version.NumeroVersion,
            version.MunicipioCodigo,
            version.Estado.ToString(),
            reporte,
            version.ErrorCarga));
    }
}
