using MediatR;
using SG.Domain.Catastro.Enums;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.AsignarPropietario;

public sealed record AsignarPropietarioPredioCommand(
    Guid PredioId,
    Guid PropietarioId,
    TipoDerecho TipoDerecho,
    decimal Porcentaje,
    DateOnly VigenteDesde) : IRequest<Result>;
