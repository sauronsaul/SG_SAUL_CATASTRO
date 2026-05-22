using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Domain.Catastro;
using SG.Domain.Common;

namespace SG.Application.Catastro.Predios.SubirDocumento;

public sealed class SubirDocumentoPredioCommandHandler(
    IPredioRepositorio predios,
    IMinioService minio,
    ICurrentUserService currentUser)
    : IRequestHandler<SubirDocumentoPredioCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        SubirDocumentoPredioCommand request,
        CancellationToken cancellationToken)
    {
        var predio = await predios.ObtenerPorIdAsync(request.PredioId, cancellationToken);
        if (predio is null)
            return Result.Failure<Guid>(PredioErrores.NoEncontrado);

        var usuarioId = currentUser.UserId ?? Guid.Empty;

        var extension = Path.GetExtension(request.NombreArchivo);
        var minioKey = $"predios/{request.PredioId}/{request.TipoDocumento}/{Guid.NewGuid()}-{Path.GetFileNameWithoutExtension(request.NombreArchivo)}{extension}";

        await minio.SubirAsync(
            request.Contenido,
            minioKey,
            request.ContentType,
            request.SizeBytes,
            cancellationToken);

        var docResult = predio.AgregarDocumento(
            request.NombreArchivo,
            request.ContentType,
            request.SizeBytes,
            minioKey,
            request.TipoDocumento,
            usuarioId);

        if (docResult.IsFailure)
            return Result.Failure<Guid>(docResult.Error);

        await predios.GuardarCambiosAsync(cancellationToken);
        return Result.Success(docResult.Value.Id);
    }
}
