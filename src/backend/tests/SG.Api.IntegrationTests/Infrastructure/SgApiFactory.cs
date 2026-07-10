using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using SG.Application.Abstractions;
using SG.Infrastructure.Persistencia;
using SG.Infrastructure.Persistencia.Interceptors;

namespace SG.Api.IntegrationTests.Infrastructure;

public sealed class SgApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public SgApiFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development: JWT no requiere HTTPS (opts.RequireHttpsMetadata = false).
        builder.UseEnvironment("Development");
        builder.UseSetting("SG_APPLY_MIGRATIONS", "true");

        // InMemoryCollection sobreescribe la configuración de Program.cs porque se
        // agrega como última fuente (mayor prioridad). Garantiza que el test siempre
        // apunte al contenedor de Testcontainers, independientemente del .env local.
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString,
                ["Jwt:Secret"]               = PostgreSqlFixture.JwtSecret,
                ["Jwt:Issuer"]               = "sg-api-test",
                ["Jwt:Audience"]             = "sg-api-test",
                ["Jwt:ExpiryMinutes"]        = "15",
                ["Jwt:RefreshExpiryDays"]    = "7",
                ["Admin:Email"]              = PostgreSqlFixture.AdminEmail,
                ["Admin:Password"]           = PostgreSqlFixture.AdminPassword,
            });
        });

        // Program.cs captura el JWT secret en una variable local ANTES de que
        // ConfigureAppConfiguration aplique el override. PostConfigure sincroniza
        // el IssuerSigningKey de validación con el mismo secret que usa JwtTokenService.
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme, opts =>
                {
                    var key = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(PostgreSqlFixture.JwtSecret));
                    opts.TokenValidationParameters.IssuerSigningKey = key;
                    opts.TokenValidationParameters.ValidIssuer = "sg-api-test";
                    opts.TokenValidationParameters.ValidAudience = "sg-api-test";
                });

            // Reemplazar MinIO con stub de no-op: los tests de auth no usan MinIO
            // y no hay contenedor MinIO disponible en el entorno de integración.
            services.AddScoped<IMinioService, NoOpMinioService>();

            // AddPersistencia en Program.cs captura la connectionString en una variable local
            // ANTES de que ConfigureAppConfiguration pueda aplicar el override del InMemoryCollection.
            // Si DotNetEnv sobreescribió ConnectionStrings__Default con el valor del .env local,
            // el DbContext quedaría apuntando a la BD local en vez del contenedor de Testcontainers.
            // Re-registrar aquí garantiza que el DbContext use SIEMPRE _connectionString (Testcontainers).
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>((sp, options) =>
            {
                options.UseNpgsql(_connectionString, npgsql =>
                {
                    npgsql.UseNetTopologySuite();
                    npgsql.MigrationsHistoryTable("__ef_migrations_history", schema: "identidad");
                    npgsql.CommandTimeout(300);
                });
                options.UseSnakeCaseNamingConvention();
                options.AddInterceptors(
                    sp.GetRequiredService<AuditableEntitiesInterceptor>(),
                    sp.GetRequiredService<AuditoriaInterceptor>());
            });
        });
    }
}
