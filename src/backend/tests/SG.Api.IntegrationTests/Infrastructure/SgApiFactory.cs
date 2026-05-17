using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

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
        });
    }
}
