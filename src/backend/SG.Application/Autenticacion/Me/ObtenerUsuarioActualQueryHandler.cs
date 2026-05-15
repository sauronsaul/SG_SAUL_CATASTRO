using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Autenticacion;
using SG.Contracts.Autenticacion;
using SG.Domain.Common;

namespace SG.Application.Autenticacion.UsuarioActual;

public sealed class ObtenerUsuarioActualQueryHandler(
    IUsuarioServicio usuarios,
    ICurrentUserService currentUser)
    : IRequestHandler<ObtenerUsuarioActualQuery, Result<UsuarioDto>>
{
    public async Task<Result<UsuarioDto>> Handle(ObtenerUsuarioActualQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        if (userId is null)
            return Result.Failure<UsuarioDto>(AutenticacionErrores.UsuarioNoEncontrado);

        var usuario = await usuarios.BuscarPorIdAsync(userId.Value, cancellationToken);
        if (usuario is null)
            return Result.Failure<UsuarioDto>(AutenticacionErrores.UsuarioNoEncontrado);

        var roles = await usuarios.ObtenerRolesAsync(usuario.Id, cancellationToken);
        return Result.Success(new UsuarioDto(usuario.Id, usuario.Email, usuario.NombreCompleto, roles));
    }
}
