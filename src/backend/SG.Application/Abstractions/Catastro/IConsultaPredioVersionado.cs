using SG.Contracts.Catastro;

namespace SG.Application.Abstractions.Catastro;

public interface IConsultaPredioVersionado
{
    Task<FichaPredioDto?> BuscarAsync(
        string municipioCodigo,
        int distrito,
        int manzana,
        int predio,
        CancellationToken cancellationToken);
}
