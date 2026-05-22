using MediatR;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.AsignarCodigoOficial;

public sealed record AsignarCodigoOficialCommand(
    Guid PredioId,
    string CodigoOficial) : IRequest<Result>;
