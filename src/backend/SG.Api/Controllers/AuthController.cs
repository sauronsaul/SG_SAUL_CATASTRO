using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SG.Application.Abstractions;
using SG.Application.Autenticacion;
using SG.Application.Autenticacion.Login;
using SG.Application.Autenticacion.Logout;
using SG.Application.Autenticacion.UsuarioActual;
using SG.Application.Autenticacion.Refresh;
using SG.Contracts.Autenticacion;
using SG.Domain.Common;

namespace SG.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(ISender sender, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var command = new LoginCommand(request.Email, request.Password, currentUser.IpOrigen ?? "unknown");
        var result = await sender.Send(command, ct);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToProblem(result.Error);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var command = new RefreshTokenCommand(request.RefreshToken, currentUser.IpOrigen ?? "unknown");
        var result = await sender.Send(command, ct);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToProblem(result.Error);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        var command = new LogoutCommand(request.RefreshToken, currentUser.IpOrigen ?? "unknown");
        var result = await sender.Send(command, ct);
        return result.IsSuccess ? NoContent() : MapErrorToProblem(result.Error);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await sender.Send(new ObtenerUsuarioActualQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : MapErrorToProblem(result.Error);
    }

    private ObjectResult MapErrorToProblem(DomainError error)
    {
        if (error == AutenticacionErrores.CuentaBloqueada)
            return Problem(detail: error.Message, statusCode: 423);

        if (error == AutenticacionErrores.UsuarioNoEncontrado)
            return Problem(detail: error.Message, statusCode: StatusCodes.Status404NotFound);

        return Problem(detail: error.Message, statusCode: StatusCodes.Status401Unauthorized);
    }
}
