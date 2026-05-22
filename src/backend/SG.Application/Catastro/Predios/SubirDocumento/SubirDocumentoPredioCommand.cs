using MediatR;
using SG.Domain.Catastro.Enums;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.SubirDocumento;

public sealed record SubirDocumentoPredioCommand(
    Guid PredioId,
    string NombreArchivo,
    string ContentType,
    long SizeBytes,
    Stream Contenido,
    TipoDocumento TipoDocumento) : IRequest<Result<Guid>>;
