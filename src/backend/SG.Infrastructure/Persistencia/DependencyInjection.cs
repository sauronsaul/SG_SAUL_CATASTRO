using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SG.Application.Abstractions;
using SG.Infrastructure.Identidad;
using SG.Infrastructure.Persistencia.Interceptors;

namespace SG.Infrastructure.Persistencia;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistencia(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' no configurada.");

        // Include Error Detail solo en desarrollo. Controlado por
        // appsettings.Development.json — nunca por .env ni por el servidor.
        // Ver ADR 0019.
        if (configuration.GetValue<bool>("Npgsql:IncludeErrorDetail"))
            connectionString += ";Include Error Detail=true";

        services.AddScoped<AuditableEntitiesInterceptor>();
        services.AddScoped<AuditoriaInterceptor>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsHistoryTable(
                    "__ef_migrations_history", schema: "identidad");
            });

            options.UseSnakeCaseNamingConvention();

            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntitiesInterceptor>(),
                sp.GetRequiredService<AuditoriaInterceptor>());
        });

        services
            .AddIdentityCore<UsuarioIdentidad>(opts =>
            {
                opts.User.RequireUniqueEmail = true;
                opts.Password.RequiredLength = 12;
                opts.Password.RequireDigit = true;
                opts.Password.RequireUppercase = true;
                opts.Password.RequireLowercase = true;
                opts.Password.RequireNonAlphanumeric = true;
                opts.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                opts.Lockout.MaxFailedAccessAttempts = 5;
            })
            .AddRoles<RolIdentidad>()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        return services;
    }
}
