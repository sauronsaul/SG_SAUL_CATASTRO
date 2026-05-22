using MediatR;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.EliminarDocumento;

public sealed record EliminarDocumentoPredioCommand(
    Guid PredioId,
    Guid DocumentoId,
    string Motivo) : IRequest<Result>;
