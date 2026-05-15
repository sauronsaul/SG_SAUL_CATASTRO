namespace SG.Application.Abstractions.Autenticacion;

public interface IUsuarioServicio
{
    Task<UsuarioAutenticadoDto?> BuscarPorEmailAsync(string email, CancellationToken ct = default);
    Task<UsuarioAutenticadoDto?> BuscarPorIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> VerificarPasswordAsync(Guid userId, string password, CancellationToken ct = default);
    Task RegistrarAccesoFallidoAsync(Guid userId, CancellationToken ct = default);
    Task ResetearAccesoFallidoAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ObtenerRolesAsync(Guid userId, CancellationToken ct = default);
}
