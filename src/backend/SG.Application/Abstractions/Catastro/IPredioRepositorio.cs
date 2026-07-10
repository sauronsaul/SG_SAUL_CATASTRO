using SG.Application.Common;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;

namespace SG.Application.Abstractions.Catastro;

public interface IPredioRepositorio
{
    Task<Predio?> ObtenerPorIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> ExisteCodigoCatastralAsync(string codigoCatastral, CancellationToken ct = default);
    Task<bool> ExisteTripleteCatastralAsync(int codUv, int codMan, int codPred, CancellationToken ct = default);
    Task<PagedResult<Predio>> ListarAsync(int page, int pageSize, CancellationToken ct = default);
    void Agregar(Predio predio);
    Task GuardarCambiosAsync(CancellationToken ct = default);

    // Consulta masiva para preview de importación: devuelve el estado actual de cada predio
    // identificado por tripleta catastral (zona, manzana, lote). Las tripletas no encontradas
    // no aparecen en el resultado — la ausencia implica AccionPreviewFila.Crear.
    Task<IReadOnlyDictionary<(string Zona, string Manzana, string Lote), EstadoPredio>>
        ObtenerEstadosPorTripletasAsync(
            IReadOnlyCollection<(string Zona, string Manzana, string Lote)> tripletas,
            CancellationToken ct = default);

    // Consulta masiva para confirmación de importación: devuelve entidades completas CON
    // tracking de EF Core para que los cambios se persistan en el SaveChangesAsync final.
    Task<Dictionary<(string Zona, string Manzana, string Lote), Predio>>
        ObtenerParaActualizarPorTripletasAsync(
            IReadOnlyCollection<(string Zona, string Manzana, string Lote)> tripletas,
            CancellationToken ct = default);
}
