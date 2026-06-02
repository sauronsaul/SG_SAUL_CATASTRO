using MediatR;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Perfiles;

public sealed record ListarPerfilesQuery
    : IRequest<Result<IReadOnlyList<PerfilImportacionResumenDto>>>;
