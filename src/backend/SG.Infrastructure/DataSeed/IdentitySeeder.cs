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
        if (existing is null)
        {
            var admin = new UsuarioIdentidad
            {
                UserName       = adminEmail,
                Email          = adminEmail,
                NombreCompleto = "Administrador del Sistema",
                EmailConfirmed = true,
                CreatedAt      = DateTime.UtcNow,
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Error al crear usuario admin: {string.Join(", ", result.Errors.Select(e => e.Description))}");

            await userManager.AddToRoleAsync(admin, "Admin");
            LogAdminCreado(logger, adminEmail);
        }
        else
        {
            LogAdminYaExiste(logger, adminEmail);
        }

        // ── Técnico inicial (opcional) ────────────────────────────────────
        var tecnicoEmail    = config["Tecnico:Email"];
        var tecnicoPassword = config["Tecnico:Password"];

        bool tieneEmail    = !string.IsNullOrWhiteSpace(tecnicoEmail);
        bool tienePassword = !string.IsNullOrWhiteSpace(tecnicoPassword);

        if (!tieneEmail && !tienePassword)
        {
            LogTecnicoNoConfigurado(logger);
            return;
        }

        if (tieneEmail ^ tienePassword)
        {
            var faltante = tieneEmail ? "TECNICO_INITIAL_PASSWORD" : "TECNICO_INITIAL_EMAIL";
            LogTecnicoConfiguracionIncompleta(logger, faltante);
            return;
        }

        var tecnicoExistente = await userManager.FindByEmailAsync(tecnicoEmail!);
        if (tecnicoExistente is not null)
        {
            LogTecnicoYaExiste(logger, tecnicoEmail!);
            return;
        }

        var tecnico = new UsuarioIdentidad
        {
            UserName       = tecnicoEmail,
            Email          = tecnicoEmail,
            NombreCompleto = "Técnico Catastral",
            EmailConfirmed = true,
            CreatedAt      = DateTime.UtcNow,
        };

        var tecnicoResult = await userManager.CreateAsync(tecnico, tecnicoPassword!);
        if (!tecnicoResult.Succeeded)
            throw new InvalidOperationException(
                $"Error al crear usuario tecnico: {string.Join(", ", tecnicoResult.Errors.Select(e => e.Description))}");

        await userManager.AddToRoleAsync(tecnico, "Tecnico");
        LogTecnicoCreado(logger, tecnicoEmail!);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Rol creado: {Rol}")]
    private static partial void LogRolCreado(ILogger logger, string rol);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuario admin ya existe: {Email}")]
    private static partial void LogAdminYaExiste(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Usuario admin creado: {Email}")]
    private static partial void LogAdminCreado(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "TECNICO_INITIAL_EMAIL / TECNICO_INITIAL_PASSWORD no configurados. " +
                  "El sistema arrancará sin tecnico inicial; puede crearse despues via API.")]
    private static partial void LogTecnicoNoConfigurado(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Configuracion de tecnico inicial incompleta: {Faltante} no esta definido. " +
                  "Tecnico inicial omitido. Configure ambas variables " +
                  "(TECNICO_INITIAL_EMAIL y _PASSWORD) o ninguna.")]
    private static partial void LogTecnicoConfiguracionIncompleta(ILogger logger, string faltante);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tecnico inicial {Email} ya existe; omitiendo creacion.")]
    private static partial void LogTecnicoYaExiste(ILogger logger, string email);

    [LoggerMessage(Level = LogLevel.Information, Message = "Tecnico inicial {Email} creado.")]
    private static partial void LogTecnicoCreado(ILogger logger, string email);
}
