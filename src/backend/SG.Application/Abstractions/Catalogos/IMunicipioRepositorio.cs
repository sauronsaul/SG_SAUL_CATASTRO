namespace SG.Application.Abstractions.Catalogos;

public interface IMunicipioRepositorio
{
    Task<bool> ExistePorCodigoIneAsync(
        string codigoIne,
        CancellationToken ct = default);
}
