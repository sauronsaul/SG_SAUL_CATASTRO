using MediatR;
using SG.Contracts.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.BuscarPorTriplete;

public sealed record BuscarFichaPredioQuery(int Distrito, int Manzana, int Predio)
    : IRequest<Result<FichaPredioDto>>;
