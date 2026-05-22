using MediatR;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.AsignarGeometria;

public sealed record AsignarGeometriaPredioCommand(
    Guid PredioId,
    string Geometria,
    string Formato,
    int? Srid) : IRequest<Result>;
