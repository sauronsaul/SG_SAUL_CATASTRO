using MediatR;
using Microsoft.Extensions.Options;
using SG.Application.Abstractions.Catastro;
using SG.Application.Catastro.Config;
using SG.Domain.Catalogos;
using SG.Domain.Catastro;
using SG.Domain.Catastro.ValueObjects;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.RegistrarPredio;

public sealed class RegistrarPredioCommandHandler(
    IPredioRepositorio predios,
    IUsoSueloRepositorio usosSuelo,
    IOptions<CatastroConfig> config)
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
            config.Value.MunicipioCodigo,
            ubicacionResult.Value,
            request.SuperficieDeclarada,
            request.UsoSueloId,
            Guid.Empty);

        if (predioResult.IsFailure)
            return Result.Failure<Guid>(predioResult.Error);

        if (await predios.ExisteTripleteCatastralAsync(
                config.Value.MunicipioCodigo,
                predioResult.Value.CodUv,
                predioResult.Value.CodMan,
                predioResult.Value.CodPred,
                cancellationToken))
            return Result.Failure<Guid>(PredioErrores.TripleteCatastralDuplicado);

        predios.Agregar(predioResult.Value);
        await predios.GuardarCambiosAsync(cancellationToken);

        return Result.Success(predioResult.Value.Id);
    }
}
