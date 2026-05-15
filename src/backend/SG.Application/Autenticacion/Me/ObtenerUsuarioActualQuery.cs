using MediatR;
using SG.Contracts.Autenticacion;
using SG.Domain.Common;

namespace SG.Application.Autenticacion.UsuarioActual;

public sealed record ObtenerUsuarioActualQuery : IRequest<Result<UsuarioDto>>;
