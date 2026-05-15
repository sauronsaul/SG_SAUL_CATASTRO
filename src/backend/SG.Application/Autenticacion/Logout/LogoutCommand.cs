using MediatR;
using SG.Domain.Common;

namespace SG.Application.Autenticacion.Logout;

public sealed record LogoutCommand(
    string RefreshToken,
    string IpOrigen) : IRequest<Result>;
