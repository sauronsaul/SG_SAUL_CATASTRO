using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SG.Infrastructure.Identidad;

namespace SG.Infrastructure.DataSeed;

public static partial class IdentitySeeder
{
    public static async Task SeedAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var config = sp.GetRequiredService<IConfiguration>();

        var adminEmail = config["Admin:Email"];
        var adminPassword = config["Admin:Password"];

        if (string.IsNullOrWhiteSpace(adminEmail))
            throw new InvalidOperationException(
                "ADMIN_INITIAL_EMAIL no configurado. " +
                "Agregue esta variable a su archivo .env antes del primer arranque.");

        if (string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException(
                "ADMIN_INITIAL_PASSWORD no configurado. " +
                "Agregue esta variable a su archivo .env antes del primer arranque.");

        var roleManager = sp.GetRequiredService<RoleManager<RolIdentidad>>();
        var userManager = sp.GetRequiredService<UserManager<UsuarioIdentidad>>();

        string[] roles = ["Admin", "Tecnico", "Operador", "Consulta"];
        foreach (var rol in roles)
        {
            if (!await roleManager.RoleExistsAsync(rol))
            {
                await roleManager.CreateAsync(new RolIdentidad { Name = rol });
                LogRolCreado(logger, rol);
            }
        }

        var existing = await userManager.FindByEmailAsync(adminEmail);
        if (existing is not null)
        {
            LogAdminYaExiste(logger, adminEmail);
            return;
        }

        var admin = new UsuarioIdentidad
        {
            UserName = adminEmail,
            Email = adminEmail,
            NombreCompleto = "Administrador del Sistema",
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow,
        };

        var result = await userManager.CreateAsync(admin, adminPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Error al crear usuario admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(admin, "Admin");
        LogAdminCreado(logger, adminEmail);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rol creado: {Rol}")]
    private static partial void LogRolCreado(ILogger logger, string rol);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuario admin ya existe: {Email}")]
    private static partial void LogAdminYaExiste(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuario admin creado: {Email}")]
    private static partial void LogAdminCreado(ILogger logger, string email);
}
