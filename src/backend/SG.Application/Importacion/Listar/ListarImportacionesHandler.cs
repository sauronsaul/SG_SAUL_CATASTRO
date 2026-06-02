using MediatR;
using SG.Application.Abstractions.Importacion;
using SG.Application.Common;
using SG.Contracts.Importacion;
using SG.Domain.Common;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Application.Importacion.Listar;

public sealed class ListarImportacionesHandler(IImportacionRepositorio importaciones)
    : IRequestHandler<ListarImportacionesQuery, Result<PagedResult<ImportacionResumenDto>>>
{
    public async Task<Result<PagedResult<ImportacionResumenDto>>> Handle(
        ListarImportacionesQuery request,
        CancellationToken cancellationToken)
    {
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var paginado = await importaciones.ListarAsync(
            page,
            pageSize,
            request.Estado,
            request.FechaDesde,
            request.FechaHasta,
            cancellationToken);

        var dtos = paginado.Items
            .Select(ToResumen)
            .ToList();

        return Result.Success(new PagedResult<ImportacionResumenDto>(
            dtos, paginado.Total, paginado.Page, paginado.PageSize));
    }

    private static ImportacionResumenDto ToResumen(ImportacionDomain.Importacion i) =>
        new(i.Id,
            i.NombreArchivo,
            i.Estado.ToString(),
            i.FechaImportacion,
            i.ImportadoPorId,
            i.PerfilId,
            i.TotalFilas,
            i.FilasImportadas,
            i.FilasConAdvertencia,
            i.FilasRechazadas,
            i.FilasOmitidas);
}
