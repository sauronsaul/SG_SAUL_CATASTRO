using MediatR;
using SG.Contracts.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.ObtenerPorId;

public sealed record ObtenerPredioPorIdQuery(Guid Id) : IRequest<Result<PredioDto>>;
