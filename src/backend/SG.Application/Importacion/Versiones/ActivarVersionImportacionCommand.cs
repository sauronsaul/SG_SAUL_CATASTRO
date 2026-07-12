using MediatR;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public sealed record ActivarVersionImportacionCommand(Guid DatasetVersionId)
    : IRequest<Result<ActivarVersionImportacionDto>>;
