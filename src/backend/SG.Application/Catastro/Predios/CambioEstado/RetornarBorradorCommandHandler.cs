using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.CambioEstado;

public sealed class RetornarBorradorCommandHandler(
    IPredioRepositorio predios,
    ICurrentUserService currentUser)
    : IRequestHandler<RetornarBorradorCommand, Result>
{
    public async Task<Result> Handle(
        RetornarBorradorCommand request,
        CancellationToken cancellationToken)
    {
        var predio = await predios.ObtenerPorIdAsync(request.PredioId, cancellationToken);
        if (predio is null)
            return Result.Failure(PredioErrores.NoEncontrado);

        var usuarioId = currentUser.UserId ?? Guid.Empty;
        var result = predio.RetornarBorrador(usuarioId);

        if (result.IsFailure)
            return result;

        await predios.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
