using MediatR;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.CambioEstado;

public sealed record EnviarARevisionCommand(Guid PredioId) : IRequest<Result>;
