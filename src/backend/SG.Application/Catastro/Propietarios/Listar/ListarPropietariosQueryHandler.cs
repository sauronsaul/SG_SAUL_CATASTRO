using MediatR;
using SG.Application.Abstractions.Catastro;
using SG.Application.Common;
using SG.Contracts.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Propietarios.Listar;

public sealed class ListarPropietariosQueryHandler(
    IPropietarioRepositorio propietarios)
    : IRequestHandler<ListarPropietariosQuery, Result<PagedResult<PropietarioDto>>>
{
    private const int PageSizeMaximo = 100;

    public async Task<Result<PagedResult<PropietarioDto>>> Handle(
        ListarPropietariosQuery request,
        CancellationToken cancellationToken)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, PageSizeMaximo);

        var paged = await propietarios.ListarAsync(page, pageSize, request.Busqueda, cancellationToken);

        var dtos = paged.Items
            .Select(MapToDto)
            .ToList();

        return Result.Success(new PagedResult<PropietarioDto>(dtos, paged.Total, paged.Page, paged.PageSize));
    }

    private static PropietarioDto MapToDto(Propietario p) => new(
        p.Id,
        p.Tipo.ToString(),
        p.NombreCompleto,
        p.Nombre,
        p.Apellidos,
        p.Cedula,
        p.RazonSocial,
        p.Nit,
        p.RepresentanteLegal,
        p.Email,
        p.Telefono,
        p.Direccion,
        p.CreatedAt);
}
