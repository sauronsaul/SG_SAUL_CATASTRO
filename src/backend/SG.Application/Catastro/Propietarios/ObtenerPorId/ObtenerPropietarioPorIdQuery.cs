using MediatR;
using SG.Contracts.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Propietarios.ObtenerPorId;

public sealed record ObtenerPropietarioPorIdQuery(Guid Id) : IRequest<Result<PropietarioDto>>;
