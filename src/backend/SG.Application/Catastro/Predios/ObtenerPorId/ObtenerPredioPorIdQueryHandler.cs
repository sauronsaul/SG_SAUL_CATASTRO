using MediatR;
using SG.Application.Abstractions.Catastro;
using SG.Contracts.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.ObtenerPorId;

public sealed class ObtenerPredioPorIdQueryHandler(
    IPredioRepositorio predios)
    : IRequestHandler<ObtenerPredioPorIdQuery, Result<PredioDto>>
{
    public async Task<Result<PredioDto>> Handle(
        ObtenerPredioPorIdQuery request,
        CancellationToken cancellationToken)
    {
        var predio = await predios.ObtenerPorIdAsync(request.Id, cancellationToken);

        if (predio is null)
            return Result.Failure<PredioDto>(PredioErrores.NoEncontrado);

        return Result.Success(MapToDto(predio));
    }

    private static PredioDto MapToDto(Predio p) => new(
        p.Id,
        p.CodigoCatastral?.Valor,
        p.Estado.ToString(),
        p.SuperficieDeclarada,
        p.SuperficieSig,
        p.SuperficieOficial,
        p.UsoSueloId,
        p.Ubicacion.Zona,
        p.Ubicacion.Manzana,
        p.Ubicacion.Lote,
        p.Ubicacion.Barrio,
        p.Ubicacion.Direccion,
        p.Ubicacion.Referencia,
        p.Geometria != null,
        p.Geometria?.CalcularAreaM2(),
        p.CreatedAt,
        p.Historial.Select(h => new HistorialEstadoDto(
            h.Id,
            h.EstadoAnterior.ToString(),
            h.EstadoNuevo.ToString(),
            h.CambiadoPor,
            h.CambiadoAt,
            h.Observaciones)).ToList());
}
