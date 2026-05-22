using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.EliminarDocumento;

public sealed class EliminarDocumentoPredioCommandHandler(
    IPredioRepositorio predios,
    IMinioService minio,
    ICurrentUserService currentUser)
    : IRequestHandler<EliminarDocumentoPredioCommand, Result>
{
    public async Task<Result> Handle(
        EliminarDocumentoPredioCommand request,
        CancellationToken cancellationToken)
    {
        var predio = await predios.ObtenerPorIdAsync(request.PredioId, cancellationToken);
        if (predio is null)
            return Result.Failure(PredioErrores.NoEncontrado);

        var documento = predio.Documentos.FirstOrDefault(d => d.Id == request.DocumentoId);
        if (documento is null)
            return Result.Failure(DocumentoErrores.NoEncontrado);

        var minioKey = documento.MinioKey;

        var usuarioId = currentUser.UserId ?? Guid.Empty;
        var result = predio.EliminarDocumento(request.DocumentoId, usuarioId, request.Motivo);
        if (result.IsFailure)
            return result;

        await minio.MoverAPapeleraAsync(minioKey, cancellationToken);
        await predios.GuardarCambiosAsync(cancellationToken);
        return Result.Success();
    }
}
