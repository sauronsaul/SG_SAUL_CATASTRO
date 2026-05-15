using MediatR;
using SG.Contracts.Autenticacion;
using SG.Domain.Common;

namespace SG.Application.Autenticacion.Login;

public sealed record LoginCommand(
    string Email,
    string Password,
    string IpOrigen) : IRequest<Result<LoginResponse>>;
