using MediatR;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public sealed record DescartarVersionImportacionCommand(Guid DatasetVersionId)
    : IRequest<Result<DescartarVersionImportacionDto>>;
