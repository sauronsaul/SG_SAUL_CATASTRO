using MediatR;
using SG.Contracts.Autenticacion;
using SG.Domain.Common;

namespace SG.Application.Autenticacion.Refresh;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    string IpOrigen) : IRequest<Result<RefreshTokenResponse>>;
