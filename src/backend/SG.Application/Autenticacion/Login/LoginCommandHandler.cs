using MediatR;
using SG.Application.Abstractions.Autenticacion;
using SG.Contracts.Autenticacion;
using SG.Domain.Common;

namespace SG.Application.Autenticacion.Login;

public sealed class LoginCommandHandler(
    IUsuarioServicio usuarios,
    IRefreshTokenRepositorio refreshTokens,
    ITokenService tokenService,
    IAuditoriaService auditoria)
    : IRequestHandler<LoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var usuario = await usuarios.BuscarPorEmailAsync(request.Email, cancellationToken);

        if (usuario is null)
        {
            await auditoria.RegistrarAsync(
                modulo: "identidad",
                accion: "login_fallido",
                usuarioId: null,
                entidadTipo: "Usuario",
                entidadId: "desconocido",
                resultado: "ERROR",
                ipOrigen: request.IpOrigen,
                motivo: "usuario_inexistente",
                ct: cancellationToken);
            return Result.Failure<LoginResponse>(AutenticacionErrores.CredencialesInvalidas);
        }

        if (usuario.EstaBloquado)
        {
            await auditoria.RegistrarAsync(
                modulo: "identidad",
                accion: "login_fallido",
                usuarioId: usuario.Id,
                entidadTipo: "Usuario",
                entidadId: usuario.Id.ToString(),
                resultado: "ERROR",
                ipOrigen: request.IpOrigen,
                motivo: "cuenta_bloqueada",
                ct: cancellationToken);
            return Result.Failure<LoginResponse>(AutenticacionErrores.CuentaBloqueada);
        }

        var passwordValido = await usuarios.VerificarPasswordAsync(usuario.Id, request.Password, cancellationToken);
        if (!passwordValido)
        {
            await usuarios.RegistrarAccesoFallidoAsync(usuario.Id, cancellationToken);
            await auditoria.RegistrarAsync(
                modulo: "identidad",
                accion: "login_fallido",
                usuarioId: usuario.Id,
                entidadTipo: "Usuario",
                entidadId: usuario.Id.ToString(),
                resultado: "ERROR",
                ipOrigen: request.IpOrigen,
                motivo: "password_incorrecto",
                ct: cancellationToken);
            return Result.Failure<LoginResponse>(AutenticacionErrores.CredencialesInvalidas);
        }

        await usuarios.ResetearAccesoFallidoAsync(usuario.Id, cancellationToken);

        var roles = await usuarios.ObtenerRolesAsync(usuario.Id, cancellationToken);
        var (accessToken, expiresAt) = tokenService.GenerarAccessToken(
            usuario.Id, usuario.Email, usuario.NombreCompleto, roles);
        var refreshTokenStr = tokenService.GenerarRefreshToken();

        await refreshTokens.CrearAsync(usuario.Id, refreshTokenStr, request.IpOrigen, cancellationToken);

        await auditoria.RegistrarAsync(
            modulo: "identidad",
            accion: "login",
            usuarioId: usuario.Id,
            entidadTipo: "Usuario",
            entidadId: usuario.Id.ToString(),
            resultado: "OK",
            ipOrigen: request.IpOrigen,
            ct: cancellationToken);

        return Result.Success(new LoginResponse(
            accessToken,
            expiresAt,
            refreshTokenStr,
            new UsuarioDto(usuario.Id, usuario.Email, usuario.NombreCompleto, roles)));
    }
}
