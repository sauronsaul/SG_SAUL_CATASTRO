using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minio;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Application.Abstractions.Autenticacion;
using SG.Application.Abstractions.Importacion;
using SG.Infrastructure.Almacenamiento;
using SG.Infrastructure.Catastro;
using SG.Infrastructure.GIS;
using SG.Infrastructure.Importacion;
using SG.Infrastructure.Identidad;
using SG.Infrastructure.Persistencia.Interceptors;
using SG.Infrastructure.Seguridad;

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
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IUsuarioServicio, UsuarioServicio>();
        services.AddScoped<IRefreshTokenRepositorio, RefreshTokenRepositorio>();
        services.AddScoped<IAuditoriaService, AuditoriaService>();

        // Repositorios catastro
        services.AddScoped<IUsoSueloRepositorio, UsoSueloRepositorio>();
        services.AddScoped<IPropietarioRepositorio, PropietarioRepositorio>();
        services.AddScoped<IPredioRepositorio, PredioRepositorio>();

        // Servicios GIS
        services.AddScoped<ICoordenadasService, CoordenadasService>();
        services.AddScoped<IGeometriaService, GeometriaService>();

        // Repositorios de importación
        services.AddScoped<IPerfilImportacionRepositorio, PerfilImportacionRepositorio>();
        services.AddScoped<IImportacionRepositorio, ImportacionRepositorio>();
        services.AddScoped<IDatasetVersionRepositorio, DatasetVersionRepositorio>();
        services.AddScoped<ICargaVersionadaServicio, CargaVersionadaServicio>();

        // Servicios de importación
        services.AddSingleton<IShapefileReader, ShapefileReader>();
        services.AddSingleton<IZipExtractor, ZipExtractor>();

        // MinIO: IMinioClient como singleton (thread-safe, costoso de construir)
        services.AddSingleton<IMinioClient>(sp =>
        {
            var cfg = sp.GetRequiredService<IOptions<MinioSettings>>().Value;
            return new MinioClient()
                .WithEndpoint(cfg.Endpoint)
                .WithCredentials(cfg.AccessKey, cfg.SecretKey)
                .WithSSL(cfg.UseSsl)
                .Build();
        });
        services.AddScoped<IMinioService, MinioService>();

        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsHistoryTable(
                    "__ef_migrations_history", schema: "identidad");
                // 300s por batch SQL: importaciones masivas (~84.000 INSERTs) pueden
                // exceder el default de 30s en hardware lento (ADR 0036).
                npgsql.CommandTimeout(300);
            });

            options.UseSnakeCaseNamingConvention();

            options.AddInterceptors(
                sp.GetRequiredService<AuditableEntitiesInterceptor>(),
                sp.GetRequiredService<AuditoriaInterceptor>());
        });

        // Registrar ANTES de AddIdentityCore para que el contenedor DI tome el custom.
        services.AddScoped<IPasswordHasher<UsuarioIdentidad>, BcryptPasswordHasher>();

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
