using SG.Contracts.Importacion;

namespace SG.Application.Abstractions.Importacion;

public interface IReportePreviewVersionServicio
{
    Task<ReportePreliminarVersionDto> GenerarAsync(
        Guid datasetVersionId,
        CancellationToken ct = default);
}
