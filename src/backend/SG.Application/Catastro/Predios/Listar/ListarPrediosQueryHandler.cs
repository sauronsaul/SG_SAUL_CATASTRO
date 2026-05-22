using MediatR;
using SG.Application.Abstractions.Catastro;
using SG.Application.Common;
using SG.Contracts.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.Listar;

public sealed class ListarPrediosQueryHandler(
    IPredioRepositorio predios)
    : IRequestHandler<ListarPrediosQuery, Result<PagedResult<PredioResumenDto>>>
{
    private const int PageSizeMaximo = 100;

    public async Task<Result<PagedResult<PredioResumenDto>>> Handle(
        ListarPrediosQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, PageSizeMaximo);

        var paged = await predios.ListarAsync(page, pageSize, cancellationToken);

        var dtos = paged.Items.Select(MapToResumen).ToList();

        return Result.Success(new PagedResult<PredioResumenDto>(dtos, paged.Total, paged.Page, paged.PageSize));
    }

    private static PredioResumenDto MapToResumen(Predio p) => new(
        p.Id,
        p.CodigoCatastral?.Valor,
        p.Estado.ToString(),
        p.SuperficieDeclarada,
        p.UsoSueloId,
        p.Ubicacion.Zona,
        p.Ubicacion.Manzana,
        p.Ubicacion.Lote,
        p.Ubicacion.Barrio,
        p.Ubicacion.Direccion,
        p.CreatedAt);
}
