using MediatR;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public sealed class DescartarVersionImportacionHandler(ICargaVersionadaServicio carga)
    : IRequestHandler<DescartarVersionImportacionCommand, Result<DescartarVersionImportacionDto>>
{
    public Task<Result<DescartarVersionImportacionDto>> Handle(
        DescartarVersionImportacionCommand request,
        CancellationToken cancellationToken) =>
        carga.DescartarYPurgarAsync(request.DatasetVersionId, cancellationToken);
}
