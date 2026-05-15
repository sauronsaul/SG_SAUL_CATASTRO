using MediatR;
using SG.Application.Abstractions.Autenticacion;
using SG.Contracts.Autenticacion;
using SG.Domain.Common;

namespace SG.Application.Autenticacion.Refresh;

public sealed class RefreshTokenCommandHandler(
    IRefreshTokenRepositorio refreshTokens,
    IUsuarioServicio usuarios,
    ITokenService tokenService,
    IAuditoriaService auditoria)
    : IRequestHandler<RefreshTokenCommand, Result<RefreshTokenResponse>>
{
    public async Task<Result<RefreshTokenResponse>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var tokenDto = await refreshTokens.BuscarPorTokenAsync(request.RefreshToken, cancellationToken);

        if (tokenDto is null)
            return Result.Failure<RefreshTokenResponse>(AutenticacionErrores.TokenInvalido);

        if (!tokenDto.IsActive)
        {
            if (tokenDto.EstaRevocado)
            {
                await refreshTokens.RevocarTodosAsync(
                    tokenDto.UsuarioId, request.IpOrigen, "reutilizacion_detectada", cancellationToken);
                await auditoria.RegistrarAsync(
                    modulo: "identidad",
                    accion: "reutilizacion_detectada",
                    usuarioId: tokenDto.UsuarioId,
                    entidadTipo: "RefreshToken",
                    entidadId: tokenDto.Id.ToString(),
                    resultado: "ERROR",
                    ipOrigen: request.IpOrigen,
                    ct: cancellationToken);
                return Result.Failure<RefreshTokenResponse>(AutenticacionErrores.ReutilizacionDetectada);
            }

            return Result.Failure<RefreshTokenResponse>(AutenticacionErrores.TokenExpirado);
        }

        var usuario = await usuarios.BuscarPorIdAsync(tokenDto.UsuarioId, cancellationToken);
        if (usuario is null)
            return Result.Failure<RefreshTokenResponse>(AutenticacionErrores.TokenInvalido);

        var roles = await usuarios.ObtenerRolesAsync(usuario.Id, cancellationToken);
        var (accessToken, expiresAt) = tokenService.GenerarAccessToken(
            usuario.Id, usuario.Email, usuario.NombreCompleto, roles);
        var nuevoRefreshTokenStr = tokenService.GenerarRefreshToken();

        await refreshTokens.RevocarAsync(
            tokenDto.Id, request.IpOrigen, nuevoRefreshTokenStr, "rotacion", cancellationToken);
        await refreshTokens.CrearAsync(usuario.Id, nuevoRefreshTokenStr, request.IpOrigen, cancellationToken);

        await auditoria.RegistrarAsync(
            modulo: "identidad",
            accion: "refresh_token",
            usuarioId: usuario.Id,
            entidadTipo: "RefreshToken",
            entidadId: tokenDto.Id.ToString(),
            resultado: "OK",
            ipOrigen: request.IpOrigen,
            ct: cancellationToken);

        return Result.Success(new RefreshTokenResponse(accessToken, expiresAt, nuevoRefreshTokenStr));
    }
}
