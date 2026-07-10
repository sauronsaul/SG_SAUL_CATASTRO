using MediatR;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public sealed record ObtenerEstadoVersionImportacionQuery(Guid DatasetVersionId)
    : IRequest<Result<EstadoVersionImportacionDto>>;
