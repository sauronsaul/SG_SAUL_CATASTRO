using MediatR;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.RegistrarPredio;

public sealed record RegistrarPredioCommand(
    string UbicacionZona,
    string UbicacionManzana,
    string UbicacionLote,
    string? UbicacionBarrio,
    string? UbicacionDireccion,
    string? UbicacionReferencia,
    decimal SuperficieDeclarada,
    Guid UsoSueloId) : IRequest<Result<Guid>>;
