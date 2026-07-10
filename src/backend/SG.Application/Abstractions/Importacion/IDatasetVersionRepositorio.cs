using SG.Domain.Importacion;

namespace SG.Application.Abstractions.Importacion;

public interface IDatasetVersionRepositorio
{
    Task<DatasetVersion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<DatasetVersion?> ObtenerPorIdSinTrackingAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<DatasetVersion>> ObtenerEnCargaAsync(CancellationToken ct = default);
    Task<int> ObtenerSiguienteNumeroAsync(string municipioCodigo, CancellationToken ct = default);
    void Agregar(DatasetVersion version);
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
