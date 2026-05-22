using MediatR;
using SG.Contracts.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.VerHistorial;

public sealed record VerHistorialEstadosQuery(Guid PredioId) : IRequest<Result<IReadOnlyList<HistorialEstadoDto>>>;
