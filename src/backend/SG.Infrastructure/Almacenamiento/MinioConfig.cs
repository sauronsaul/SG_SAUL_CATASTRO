namespace SG.Infrastructure.Almacenamiento;

public sealed class MinioSettings
{
    public string Endpoint { get; init; } = "localhost:9000";
    public string AccessKey { get; init; } = string.Empty;
    public string SecretKey { get; init; } = string.Empty;
    public bool UseSsl { get; init; }
    public string BucketPredios { get; init; } = "sg-predios";
    public int PresignedUrlExpiryMinutes { get; init; } = 15;
}
