using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Minio;
using Minio.DataModel.Args;
using SG.Application.Abstractions;

namespace SG.Infrastructure.Almacenamiento;

internal sealed partial class MinioService(
    IMinioClient minioClient,
    IOptions<MinioSettings> config,
    ILogger<MinioService> logger)
    : IMinioService
{
    private string Bucket => config.Value.BucketPredios;

    public Task InicializarAsync(CancellationToken ct = default) =>
        EnsureBucketExistsAsync(ct);

    public async Task<string> SubirAsync(
        Stream stream,
        string key,
        string contentType,
        long sizeBytes,
        CancellationToken ct = default)
    {
        await EnsureBucketExistsAsync(ct);

        var args = new PutObjectArgs()
            .WithBucket(Bucket)
            .WithObject(key)
            .WithStreamData(stream)
            .WithObjectSize(sizeBytes)
            .WithContentType(contentType);

        await minioClient.PutObjectAsync(args, ct);
        LogSubida(logger, key, Bucket);
        return key;
    }

    public async Task<Stream> DescargarAsync(string key, CancellationToken ct = default)
    {
        var buffer = new MemoryStream();

        var args = new GetObjectArgs()
            .WithBucket(Bucket)
            .WithObject(key)
            .WithCallbackStream(stream => stream.CopyTo(buffer));

        await minioClient.GetObjectAsync(args, ct);
        buffer.Position = 0;
        return buffer;
    }

    public async Task MoverAPapeleraAsync(string key, CancellationToken ct = default)
    {
        var destino = $"papelera/{key}";

        var copyArgs = new CopyObjectArgs()
            .WithBucket(Bucket)
            .WithObject(destino)
            .WithCopyObjectSource(new CopySourceObjectArgs()
                .WithBucket(Bucket)
                .WithObject(key));

        await minioClient.CopyObjectAsync(copyArgs, ct);

        var removeArgs = new RemoveObjectArgs()
            .WithBucket(Bucket)
            .WithObject(key);

        await minioClient.RemoveObjectAsync(removeArgs, ct);
        LogPapelera(logger, key, destino);
    }

    internal async Task EnsureBucketExistsAsync(CancellationToken ct = default)
    {
        var args = new BucketExistsArgs().WithBucket(Bucket);
        var existe = await minioClient.BucketExistsAsync(args, ct);
        if (!existe)
        {
            var makeArgs = new MakeBucketArgs().WithBucket(Bucket);
            await minioClient.MakeBucketAsync(makeArgs, ct);
            LogBucketCreado(logger, Bucket);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Documento '{Key}' subido al bucket '{Bucket}'.")]
    private static partial void LogSubida(ILogger logger, string key, string bucket);

    [LoggerMessage(Level = LogLevel.Information, Message = "Documento '{Key}' movido a papelera '{Destino}'.")]
    private static partial void LogPapelera(ILogger logger, string key, string destino);

    [LoggerMessage(Level = LogLevel.Information, Message = "Bucket '{Bucket}' creado automáticamente.")]
    private static partial void LogBucketCreado(ILogger logger, string bucket);
}
