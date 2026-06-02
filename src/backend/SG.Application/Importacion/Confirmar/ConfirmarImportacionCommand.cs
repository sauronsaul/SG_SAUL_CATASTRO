using MediatR;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Confirmar;

public sealed record ConfirmarImportacionCommand(Guid ImportacionId)
    : IRequest<Result<ConfirmacionImportacionDto>>;
