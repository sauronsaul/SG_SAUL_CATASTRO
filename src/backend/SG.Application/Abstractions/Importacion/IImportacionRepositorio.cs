using SG.Application.Common;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Application.Abstractions.Importacion;

public interface IImportacionRepositorio
{
    // Consultas de lectura (AsNoTracking)
    Task<PagedResult<ImportacionDomain.Importacion>> ListarAsync(
        int page,
        int pageSize,
        ImportacionDomain.EstadoImportacion? estado = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        CancellationToken ct = default);

    Task<ImportacionDomain.Importacion?> ObtenerPorIdSinTrackingAsync(
        Guid id, CancellationToken ct = default);

    // Escritura (con tracking)
    Task<ImportacionDomain.Importacion?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    void Agregar(ImportacionDomain.Importacion importacion);
    Task GuardarCambiosAsync(CancellationToken ct = default);
}
