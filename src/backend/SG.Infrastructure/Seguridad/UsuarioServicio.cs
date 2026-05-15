using Microsoft.AspNetCore.Identity;
using SG.Application.Abstractions.Autenticacion;
using SG.Infrastructure.Identidad;

namespace SG.Infrastructure.Seguridad;

internal sealed class UsuarioServicio(UserManager<UsuarioIdentidad> userManager)
    : IUsuarioServicio
{
    public async Task<UsuarioAutenticadoDto?> BuscarPorEmailAsync(string email, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || user.IsDeleted) return null;
        var bloqueado = await userManager.IsLockedOutAsync(user);
        return new UsuarioAutenticadoDto(user.Id, user.Email!, user.NombreCompleto, bloqueado);
    }

    public async Task<UsuarioAutenticadoDto?> BuscarPorIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(id.ToString());
        if (user is null || user.IsDeleted) return null;
        var bloqueado = await userManager.IsLockedOutAsync(user);
        return new UsuarioAutenticadoDto(user.Id, user.Email!, user.NombreCompleto, bloqueado);
    }

    public async Task<bool> VerificarPasswordAsync(Guid userId, string password, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return false;
        return await userManager.CheckPasswordAsync(user, password);
    }

    public async Task RegistrarAccesoFallidoAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return;
        await userManager.AccessFailedAsync(user);
    }

    public async Task ResetearAccesoFallidoAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return;
        await userManager.ResetAccessFailedCountAsync(user);
    }

    public async Task<IReadOnlyList<string>> ObtenerRolesAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return [];
        var roles = await userManager.GetRolesAsync(user);
        return [.. roles];
    }
}
