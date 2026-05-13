namespace SG.Application.Abstractions;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    string? Email { get; }
    string? IpOrigen { get; }
    bool IsAuthenticated { get; }
}
