using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Catastro.ValueObjects;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.AsignarCodigoOficial;

public sealed class AsignarCodigoOficialCommandHandler(
    IPredioRepositorio predios,
    ICurrentUserService currentUser)
    : IRequestHandler<AsignarCodigoOficialCommand, Result>
{
    public async Task<Result> Handle(
        AsignarCodigoOficialCommand request,
        CancellationToken cancellationToken)
    {
        // Double-auth: handler verifies Admin role (in addition to controller-level [Authorize(Roles = "Admin")])
        if (!currentUser.IsAuthenticated)
            return Result.Failure(new DomainError("Auth.NoAutenticado", "Se requiere autenticación."));

        var predio = await predios.ObtenerPorIdAsync(request.PredioId, cancellationToken);
        if (predio is null)
            return Result.Failure(PredioErrores.NoEncontrado);

        var codigoResult = CodigoCatastral.Crear(request.CodigoOficial);
        if (codigoResult.IsFailure)
            return Result.Failure(codigoResult.Error);

        if (await predios.ExisteCodigoCatastralAsync(codigoResult.Value.Valor, cancellationToken))
            return Result.Failure(PredioErrores.CodigoCatastralDuplicado);

        var usuarioId = currentUser.UserId ?? Guid.Empty;
        var result = predio.AsignarCodigoOficial(codigoResult.Value, usuarioId);

        if (result.IsFailure)
            return result;

        await predios.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
