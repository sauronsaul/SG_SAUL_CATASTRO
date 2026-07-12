namespace SG.Application.Abstractions.Importacion;

public interface ICargaVersionadaServicio
{
    Task CargarAsync(Guid datasetVersionId, CancellationToken ct = default);
    Task MarcarFallidaYPurgarAsync(Guid datasetVersionId, string errorCarga, CancellationToken ct = default);
    Task MarcarHuerfanasAlArrancarAsync(CancellationToken ct = default);
}
