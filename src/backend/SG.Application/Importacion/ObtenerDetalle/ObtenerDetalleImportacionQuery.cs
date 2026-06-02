using MediatR;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.ObtenerDetalle;

public sealed record ObtenerDetalleImportacionQuery(Guid ImportacionId)
    : IRequest<Result<ImportacionDetalleDto>>;
