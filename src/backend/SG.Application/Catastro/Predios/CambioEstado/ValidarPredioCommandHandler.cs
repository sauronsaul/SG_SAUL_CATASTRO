using MediatR;
using Microsoft.Extensions.Options;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Application.Catastro.Config;
using SG.Domain.Catastro;
using SG.Domain.Catastro.ValueObjects;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.CambioEstado;

public sealed class ValidarPredioCommandHandler(
    IPredioRepositorio predios,
    ICurrentUserService currentUser,
    IOptions<CatastroConfig> config)
    : IRequestHandler<ValidarPredioCommand, Result>
{
    public async Task<Result> Handle(
        ValidarPredioCommand request,
        CancellationToken cancellationToken)
    {
        var predio = await predios.ObtenerPorIdAsync(request.PredioId, cancellationToken);
        if (predio is null)
            return Result.Failure(PredioErrores.NoEncontrado);

        var cfg = config.Value;
        var zona = predio.Ubicacion.Zona.PadLeft(3, '0');
        var manzana = predio.Ubicacion.Manzana.PadLeft(4, '0');
        var lote = predio.Ubicacion.Lote.PadLeft(4, '0');

        var entrada = $"{cfg.DepartamentoCodigo}-{cfg.ProvinciaCodigo}-{cfg.MunicipioCodigo}-{zona}-{manzana}-{lote}";

        var codigoResult = CodigoCatastral.Crear(entrada);
        if (codigoResult.IsFailure)
            return Result.Failure(codigoResult.Error);

        if (await predios.ExisteCodigoCatastralAsync(codigoResult.Value.Valor, cancellationToken))
            return Result.Failure(PredioErrores.CodigoCatastralDuplicado);

        var usuarioId = currentUser.UserId ?? Guid.Empty;
        var result = predio.Validar(codigoResult.Value, usuarioId);

        if (result.IsFailure)
            return result;

        await predios.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
