using MediatR;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.CambioEstado;

public sealed record ObservarPredioCommand(
    Guid PredioId,
    string Observaciones) : IRequest<Result>;
