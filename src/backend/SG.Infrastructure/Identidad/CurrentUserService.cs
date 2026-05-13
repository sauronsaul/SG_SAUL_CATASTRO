using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using SG.Application.Abstractions;

namespace SG.Infrastructure.Identidad;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?.User
                .FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var guid) ? guid : null;
        }
    }

    public string? Email =>
        _httpContextAccessor.HttpContext?.User
            .FindFirst(ClaimTypes.Email)?.Value;

    public string? IpOrigen =>
        _httpContextAccessor.HttpContext?
            .Connection.RemoteIpAddress?.ToString();

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User
            .Identity?.IsAuthenticated ?? false;
}
