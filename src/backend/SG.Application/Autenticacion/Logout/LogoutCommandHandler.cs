using MediatR;
using SG.Application.Abstractions.Autenticacion;
using SG.Domain.Common;

namespace SG.Application.Autenticacion.Logout;

public sealed class LogoutCommandHandler(
    IRefreshTokenRepositorio refreshTokens,
    IAuditoriaService auditoria)
    : IRequestHandler<LogoutCommand, Result>
{
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var tokenDto = await refreshTokens.BuscarPorTokenAsync(request.RefreshToken, cancellationToken);

        if (tokenDto is not null && tokenDto.IsActive)
        {
            await refreshTokens.RevocarAsync(
                tokenDto.Id, request.IpOrigen, null, "logout", cancellationToken);
            await auditoria.RegistrarAsync(
                modulo: "identidad",
                accion: "logout",
                usuarioId: tokenDto.UsuarioId,
                entidadTipo: "RefreshToken",
                entidadId: tokenDto.Id.ToString(),
                resultado: "OK",
                ipOrigen: request.IpOrigen,
                ct: cancellationToken);
        }
        else
        {
            await auditoria.RegistrarAsync(
                modulo: "identidad",
                accion: "logout",
                usuarioId: tokenDto?.UsuarioId,
                entidadTipo: "RefreshToken",
                entidadId: tokenDto?.Id.ToString() ?? "desconocido",
                resultado: "OK",
                ipOrigen: request.IpOrigen,
                motivo: "token_inexistente",
                ct: cancellationToken);
        }

        return Result.Success();
    }
}
