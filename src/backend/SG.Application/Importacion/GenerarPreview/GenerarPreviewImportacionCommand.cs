using MediatR;
using SG.Contracts.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.GenerarPreview;

public sealed record GenerarPreviewImportacionCommand(
    Guid PerfilId,
    string NombreArchivo,
    Stream ZipStream,
    long ZipSizeBytes)
    : IRequest<Result<PreviewImportacionDto>>;
