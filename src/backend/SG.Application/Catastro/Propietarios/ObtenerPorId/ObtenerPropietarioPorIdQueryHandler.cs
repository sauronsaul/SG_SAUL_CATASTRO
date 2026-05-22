using MediatR;
using SG.Application.Abstractions.Catastro;
using SG.Contracts.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Propietarios.ObtenerPorId;

public sealed class ObtenerPropietarioPorIdQueryHandler(
    IPropietarioRepositorio propietarios)
    : IRequestHandler<ObtenerPropietarioPorIdQuery, Result<PropietarioDto>>
{
    public async Task<Result<PropietarioDto>> Handle(
        ObtenerPropietarioPorIdQuery request,
        CancellationToken cancellationToken)
    {
        var propietario = await propietarios.ObtenerPorIdAsync(request.Id, cancellationToken);

        if (propietario is null)
            return Result.Failure<PropietarioDto>(PropietarioErrores.NoEncontrado);

        return Result.Success(MapToDto(propietario));
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
