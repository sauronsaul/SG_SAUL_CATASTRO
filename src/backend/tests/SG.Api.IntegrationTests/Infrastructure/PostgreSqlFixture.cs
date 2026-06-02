using Microsoft.EntityFrameworkCore;
using SG.Infrastructure.Persistencia;
using Testcontainers.PostgreSql;

namespace SG.Api.IntegrationTests.Infrastructure;

[CollectionDefinition("Postgres")]
public class PostgresFixtureGroup : ICollectionFixture<PostgreSqlFixture> { }

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public const string AdminEmail = "admin@test.local";
    public const string AdminPassword = "TestAdmin!Password123";
    internal const string JwtSecret = "integracion-test-secret-32chars-o-mas!!";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgis/postgis:16-3.4")
        .WithDatabase("sg_catastro_test")
        .WithUsername("sg_test")
        .WithPassword("Sg!TestPass123")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // Env vars deben estar antes de que cualquier SgApiFactory dispare Program.cs.
        // DotNetEnv en Program.cs solo sobrescribe vars que NO existan ya en el proceso.
        Environment.SetEnvironmentVariable("JWT_SECRET", JwtSecret);
        Environment.SetEnvironmentVariable("JWT_ISSUER", "sg-api-test");
        Environment.SetEnvironmentVariable("JWT_AUDIENCE", "sg-api-test");
        Environment.SetEnvironmentVariable("JWT_EXPIRY_MINUTES", "15");
        Environment.SetEnvironmentVariable("REFRESH_TOKEN_EXPIRY_DAYS", "7");
        Environment.SetEnvironmentVariable("ADMIN_INITIAL_EMAIL", AdminEmail);
        Environment.SetEnvironmentVariable("ADMIN_INITIAL_PASSWORD", AdminPassword);
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", ConnectionString);

        // Aplicar migraciones con un contexto standalone (sin interceptores de auditoría).
        // Debe ejecutarse ANTES de que SgApiFactory arranque el app (que corre el Seeder).
        await AplicarMigracionesAsync();

        // Limpiar datos de dominio para garantizar aislamiento entre ejecuciones.
        // Necesario porque en Windows Docker Desktop, Ryuk no destruye contenedores
        // detenidos; StartAsync() reutiliza el contenedor con datos previos.
        await LimpiarDatosDominioAsync();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private DbContextOptions<ApplicationDbContext> BuildOptions() =>
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(ConnectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.MigrationsHistoryTable("__ef_migrations_history", "identidad");
            })
            .UseSnakeCaseNamingConvention()
            .Options;

    private async Task AplicarMigracionesAsync()
    {
        await using var ctx = new ApplicationDbContext(BuildOptions());
        // EnsureDeletedAsync garantiza esquema limpio en contenedores reutilizados por
        // Windows Docker Desktop (Ryuk no destruye contenedores detenidos). Sin esto,
        // una migración regenerada con distinto timestamp falla al intentar CREATE TABLE
        // sobre tablas que ya existen de la ejecución anterior.
        await ctx.Database.EnsureDeletedAsync();
        await ctx.Database.MigrateAsync();
    }

    private async Task LimpiarDatosDominioAsync()
    {
        await using var ctx = new ApplicationDbContext(BuildOptions());
        // CASCADE elimina documentos, historial_estados y relaciones_predio_propietario.
        // La tabla identidad.usuarios (admin seed) no se toca.
        await ctx.Database.ExecuteSqlRawAsync(
            "TRUNCATE dominio.predios, dominio.propietarios RESTART IDENTITY CASCADE");

        // Verificación post-truncate: garantiza aislamiento entre ejecuciones de test.
        var prediosRestantes = await ctx.Predios.IgnoreQueryFilters().CountAsync();
        if (prediosRestantes > 0)
            throw new InvalidOperationException(
                $"TRUNCATE falló: {prediosRestantes} predio(s) siguen en la BD tras limpiar.");
    }
}
