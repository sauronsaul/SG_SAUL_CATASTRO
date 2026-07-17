using SG.Contracts.GIS;

namespace SG.Application.Abstractions.GIS;

public interface IExtensionMunicipalService
{
    Task<LimitesVisorDto?> ObtenerAsync(
        string municipioCodigo,
        Guid datasetVersionId,
        CancellationToken cancellationToken);
}
