using MediatR;
using SG.Application.Common;
using SG.Contracts.Importacion;
using SG.Domain.Common;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Application.Importacion.Listar;

public sealed record ListarImportacionesQuery(
    int Page = 1,
    int PageSize = 20,
    ImportacionDomain.EstadoImportacion? Estado = null,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null)
    : IRequest<Result<PagedResult<ImportacionResumenDto>>>;
