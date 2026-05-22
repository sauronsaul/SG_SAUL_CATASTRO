using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.AsignarGeometria;

public sealed class AsignarGeometriaPredioCommandHandler(
    IPredioRepositorio predios,
    IGeometriaService geometriaService,
    ICurrentUserService currentUser)
    : IRequestHandler<AsignarGeometriaPredioCommand, Result>
{
    public async Task<Result> Handle(
        AsignarGeometriaPredioCommand request,
        CancellationToken cancellationToken)
    {
        var predio = await predios.ObtenerPorIdAsync(request.PredioId, cancellationToken);
        if (predio is null)
            return Result.Failure(PredioErrores.NoEncontrado);

        var geometriaResult = await geometriaService.ParsearAsync(
            request.Geometria,
            request.Formato,
            request.Srid,
            cancellationToken);

        if (geometriaResult.IsFailure)
            return Result.Failure(geometriaResult.Error);

        var usuarioId = currentUser.UserId ?? Guid.Empty;
        var asignarResult = predio.AsignarGeometria(geometriaResult.Value, usuarioId);

        if (asignarResult.IsFailure)
            return asignarResult;

        await predios.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
