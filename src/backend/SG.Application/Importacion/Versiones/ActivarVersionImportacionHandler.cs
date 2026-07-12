using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public sealed class ActivarVersionImportacionHandler(
    IActivacionVersionServicio activacion,
    ICurrentUserService currentUser)
    : IRequestHandler<ActivarVersionImportacionCommand, Result<ActivarVersionImportacionDto>>
{
    public Task<Result<ActivarVersionImportacionDto>> Handle(
        ActivarVersionImportacionCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } usuarioId)
        {
            return Task.FromResult(Result.Failure<ActivarVersionImportacionDto>(
                VersionImportacionErrores.UsuarioNoDisponible));
        }

        return activacion.ActivarAsync(request.DatasetVersionId, usuarioId, cancellationToken);
    }
}
