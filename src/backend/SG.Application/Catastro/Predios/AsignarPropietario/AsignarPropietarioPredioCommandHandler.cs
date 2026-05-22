using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.AsignarPropietario;

public sealed class AsignarPropietarioPredioCommandHandler(
    IPredioRepositorio predios,
    IPropietarioRepositorio propietarios,
    ICurrentUserService currentUser)
    : IRequestHandler<AsignarPropietarioPredioCommand, Result>
{
    public async Task<Result> Handle(
        AsignarPropietarioPredioCommand request,
        CancellationToken cancellationToken)
    {
        var predio = await predios.ObtenerPorIdAsync(request.PredioId, cancellationToken);
        if (predio is null)
            return Result.Failure(PredioErrores.NoEncontrado);

        var propietarioExiste = await propietarios.ObtenerPorIdAsync(request.PropietarioId, cancellationToken);
        if (propietarioExiste is null)
            return Result.Failure(PropietarioErrores.NoEncontrado);

        var usuarioId = currentUser.UserId ?? Guid.Empty;
        var result = predio.AsignarPropietario(
            request.PropietarioId,
            request.TipoDerecho,
            request.Porcentaje,
            request.VigenteDesde,
            usuarioId);

        if (result.IsFailure)
            return result;

        await predios.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
