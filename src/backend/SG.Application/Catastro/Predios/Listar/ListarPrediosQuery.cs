using MediatR;
using SG.Application.Common;
using SG.Contracts.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.Listar;

public sealed record ListarPrediosQuery(
    int Page = 1,
    int PageSize = 20) : IRequest<Result<PagedResult<PredioResumenDto>>>;
