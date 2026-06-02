using MediatR;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Perfiles;

public sealed class ListarPerfilesHandler(IPerfilImportacionRepositorio perfiles)
    : IRequestHandler<ListarPerfilesQuery, Result<IReadOnlyList<PerfilImportacionResumenDto>>>
{
    public async Task<Result<IReadOnlyList<PerfilImportacionResumenDto>>> Handle(
        ListarPerfilesQuery request,
        CancellationToken cancellationToken)
    {
        var lista = await perfiles.ListarAsync(cancellationToken);

        var dtos = lista
            .Select(p => new PerfilImportacionResumenDto(
                p.Id,
                p.Nombre,
                p.Descripcion,
                p.TipoCapa.ToString(),
                p.Mapeos.Count))
            .ToList();

        return Result.Success<IReadOnlyList<PerfilImportacionResumenDto>>(dtos);
    }
}
