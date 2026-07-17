using MediatR;
using SG.Application.Abstractions.Catalogos;
using SG.Contracts.GIS;

namespace SG.Application.GIS.Visor;

public sealed record ListarMunicipiosVisorQuery : IRequest<IReadOnlyList<MunicipioVisorDto>>;

internal sealed class ListarMunicipiosVisorQueryHandler(IMunicipioRepositorio repositorio)
    : IRequestHandler<ListarMunicipiosVisorQuery, IReadOnlyList<MunicipioVisorDto>>
{
    public async Task<IReadOnlyList<MunicipioVisorDto>> Handle(
        ListarMunicipiosVisorQuery request,
        CancellationToken cancellationToken) =>
        (await repositorio.ListarConDatasetActivoAsync(cancellationToken))
            .Select(x => new MunicipioVisorDto(x.CodigoIne, x.Nombre, x.NombreOficial))
            .ToArray();
}
