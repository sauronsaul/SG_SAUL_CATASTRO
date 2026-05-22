namespace SG.Application.Abstractions;

public interface IMinioService
{
    /// <summary>Crea el bucket de predios si no existe. Llamar una vez al arrancar.</summary>
    Task InicializarAsync(CancellationToken ct = default);

    Task<string> SubirAsync(
        Stream stream,
        string key,
        string contentType,
        long sizeBytes,
        CancellationToken ct = default);

    Task<Stream> DescargarAsync(string key, CancellationToken ct = default);

    /// <summary>Mueve el objeto a papelera/{key}. Nunca elimina físicamente.</summary>
    Task MoverAPapeleraAsync(string key, CancellationToken ct = default);
}
