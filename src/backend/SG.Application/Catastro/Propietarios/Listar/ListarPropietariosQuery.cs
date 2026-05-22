using MediatR;
using SG.Application.Common;
using SG.Contracts.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Propietarios.Listar;

public sealed record ListarPropietariosQuery(
    int Page = 1,
    int PageSize = 20,
    string? Busqueda = null) : IRequest<Result<PagedResult<PropietarioDto>>>;
