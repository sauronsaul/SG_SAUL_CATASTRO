using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Contracts.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.VerHistorial;

public sealed class VerHistorialEstadosQueryHandler(
    IPredioRepositorio predios,
    ICurrentUserService currentUser)
    : IRequestHandler<VerHistorialEstadosQuery, Result<IReadOnlyList<HistorialEstadoDto>>>
{
    public async Task<Result<IReadOnlyList<HistorialEstadoDto>>> Handle(
        VerHistorialEstadosQuery request,
        CancellationToken cancellationToken)
    {
        // Double-auth: handler verifies authentication (controller verifies Admin role)
        if (!currentUser.IsAuthenticated)
            return Result.Failure<IReadOnlyList<HistorialEstadoDto>>(
                new DomainError("Auth.NoAutenticado", "Se requiere autenticación."));

        var predio = await predios.ObtenerPorIdAsync(request.PredioId, cancellationToken);
        if (predio is null)
            return Result.Failure<IReadOnlyList<HistorialEstadoDto>>(PredioErrores.NoEncontrado);

        var dtos = predio.Historial
            .OrderBy(h => h.CambiadoAt)
            .Select(h => new HistorialEstadoDto(
                h.Id,
                h.EstadoAnterior.ToString(),
                h.EstadoNuevo.ToString(),
                h.CambiadoPor,
                h.CambiadoAt,
                h.Observaciones))
            .ToList();

        return Result.Success<IReadOnlyList<HistorialEstadoDto>>(dtos);
    }
}
