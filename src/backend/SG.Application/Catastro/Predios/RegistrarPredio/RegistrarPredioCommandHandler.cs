using MediatR;
using SG.Application.Abstractions.Catastro;
using SG.Domain.Catalogos;
using SG.Domain.Catastro;
using SG.Domain.Catastro.ValueObjects;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.RegistrarPredio;

public sealed class RegistrarPredioCommandHandler(
    IPredioRepositorio predios,
    IUsoSueloRepositorio usosSuelo)
    : IRequestHandler<RegistrarPredioCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        RegistrarPredioCommand request,
        CancellationToken cancellationToken)
    {
        if (!await usosSuelo.ExisteAsync(request.UsoSueloId, cancellationToken))
            return Result.Failure<Guid>(UsoSueloErrores.NoEncontrado);

        var ubicacionResult = UbicacionCatastral.Crear(
            request.UbicacionZona,
            request.UbicacionManzana,
            request.UbicacionLote,
            request.UbicacionBarrio,
            request.UbicacionDireccion,
            request.UbicacionReferencia);

        if (ubicacionResult.IsFailure)
            return Result.Failure<Guid>(ubicacionResult.Error);

        var predioResult = Predio.Crear(
            ubicacionResult.Value,
            request.SuperficieDeclarada,
            request.UsoSueloId,
            Guid.Empty);

        if (predioResult.IsFailure)
            return Result.Failure<Guid>(predioResult.Error);

        predios.Agregar(predioResult.Value);
        await predios.GuardarCambiosAsync(cancellationToken);

        return Result.Success(predioResult.Value.Id);
    }
}
