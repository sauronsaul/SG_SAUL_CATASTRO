using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Abstractions.Importacion;

public interface IActivacionVersionServicio
{
    Task<Result<ActivarVersionImportacionDto>> ActivarAsync(
        Guid datasetVersionId,
        Guid usuarioId,
        CancellationToken ct = default);
}
