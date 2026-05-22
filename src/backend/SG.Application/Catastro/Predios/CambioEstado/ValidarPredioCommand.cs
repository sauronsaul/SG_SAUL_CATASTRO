using MediatR;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.CambioEstado;

public sealed record ValidarPredioCommand(Guid PredioId) : IRequest<Result>;
