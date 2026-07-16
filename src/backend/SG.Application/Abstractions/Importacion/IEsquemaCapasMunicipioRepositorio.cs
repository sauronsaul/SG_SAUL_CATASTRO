using SG.Domain.Importacion;

namespace SG.Application.Abstractions.Importacion;

public interface IEsquemaCapasMunicipioRepositorio
{
    Task<IReadOnlyList<EsquemaCapaMunicipio>> ListarAsync(
        string municipioCodigo,
        CancellationToken ct = default);
}
