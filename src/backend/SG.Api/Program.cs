using System.Data.Common;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using SG.Api.Startup;
using SG.Api.Importacion;
using SG.Application;
using SG.Application.Catastro.Config;
using SG.Infrastructure.Almacenamiento;
using SG.Infrastructure.DataSeed;
using SG.Infrastructure.Persistencia;

// Cargar .env en Development buscando desde el directorio actual hacia arriba.
var dir = Directory.GetCurrentDirectory();
while (!string.IsNullOrEmpty(dir))
{
    var envFile = Path.Combine(dir, ".env");
    if (File.Exists(envFile))
    {
        DotNetEnv.Env.Load(envFile);
        break;
    }
    dir = Path.GetDirectoryName(dir)!;
}

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();

// Mapear vars de entorno a secciones de IConfiguration.
// ASP.NET Core no resuelve ${placeholders} en appsettings.json automáticamente.
// Solo se agregan claves cuyo valor de env var no sea null, para no sobreescribir
// los defaults de appsettings.json con null (que rompería IOptions<T> binding).
var envMappings = new Dictionary<string, string?>
{
    ["Jwt:Secret"]            = Environment.GetEnvironmentVariable("JWT_SECRET"),
    ["Jwt:Issuer"]            = Environment.GetEnvironmentVariable("JWT_ISSUER"),
    ["Jwt:Audience"]          = Environment.GetEnvironmentVariable("JWT_AUDIENCE"),
    ["Jwt:ExpiryMinutes"]     = Environment.GetEnvironmentVariable("JWT_EXPIRY_MINUTES"),
    ["Jwt:RefreshExpiryDays"] = Environment.GetEnvironmentVariable("REFRESH_TOKEN_EXPIRY_DAYS"),
    ["Admin:Email"]           = Environment.GetEnvironmentVariable("ADMIN_INITIAL_EMAIL"),
    ["Admin:Password"]        = Environment.GetEnvironmentVariable("ADMIN_INITIAL_PASSWORD"),
    ["Tecnico:Email"]         = Environment.GetEnvironmentVariable("TECNICO_INITIAL_EMAIL"),
    ["Tecnico:Password"]      = Environment.GetEnvironmentVariable("TECNICO_INITIAL_PASSWORD"),
    ["Minio:Endpoint"]        = Environment.GetEnvironmentVariable("MINIO_ENDPOINT"),
    ["Minio:AccessKey"]       = Environment.GetEnvironmentVariable("MINIO_ROOT_USER"),
    ["Minio:SecretKey"]       = Environment.GetEnvironmentVariable("MINIO_ROOT_PASSWORD"),
    ["Minio:UseSsl"]          = Environment.GetEnvironmentVariable("MINIO_USE_SSL"),
    ["Minio:BucketPredios"]   = Environment.GetEnvironmentVariable("MINIO_BUCKET_PREDIOS"),
};
builder.Configuration.AddInMemoryCollection(
    envMappings.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value));

// La cadena completa de ConnectionStrings__Default (por ejemplo, la cargada desde
// .env en Docker) tiene prioridad. El fallback de Development nunca versiona
// contraseñas: completa appsettings.Development.json solo con SG_DB_PASSWORD.
var connectionString = builder.Configuration.GetConnectionString("Default");
if (builder.Environment.IsDevelopment() &&
    !MigrationStartupGuard.TienePassword(connectionString) &&
    !string.IsNullOrWhiteSpace(connectionString))
{
    var claveDb = Environment.GetEnvironmentVariable("SG_DB_PASSWORD");
    if (string.IsNullOrWhiteSpace(claveDb))
    {
        throw new InvalidOperationException(
            "SG_DB_PASSWORD no configurada para la cadena de desarrollo. " +
            "Consulte docs/DESARROLLO_NATIVO.md.");
    }

    var connectionStringBuilder = new DbConnectionStringBuilder
    {
        ConnectionString = connectionString,
    };
    connectionStringBuilder["Password"] = claveDb;
    builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["ConnectionStrings:Default"] = connectionStringBuilder.ConnectionString,
    });
}

// Fail-fast: JWT_SECRET es obligatorio (ADR 0018).
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
    throw new InvalidOperationException(
        "JWT_SECRET no configurado. " +
        "Ejecute scripts/generate-jwt-secret.ps1 y agréguelo al archivo .env.");

builder.Host.UseSerilog((ctx, services, cfg) =>
{
    cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(formatProvider: null)
        .WriteTo.File(
            path: "logs/sg-api-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30,
            formatProvider: null);
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddAuthorization();
builder.Services.AddPersistencia(builder.Configuration);
builder.Services.AddAplicacion();
builder.Services.AddHostedService<CargaVersionadaBackgroundService>();
builder.Services.Configure<CatastroConfig>(builder.Configuration.GetSection("Catastro"));
builder.Services.Configure<MinioSettings>(builder.Configuration.GetSection("Minio"));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.Zero,  // ADR 0018: sin tolerancia
        };
        opts.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

// Las migraciones solo se aplican con autorización explícita. Docker declara
// SG_APPLY_MIGRATIONS=true; dotnet run queda protegido por defecto.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (MigrationStartupGuard.DebeAplicarMigraciones(
            builder.Configuration["SG_APPLY_MIGRATIONS"]))
    {
        MigrationStartupGuard.LogAplicacionMigraciones(app.Logger);
        await db.Database.MigrateAsync();
    }
    else
    {
        var pendingMigrations = await db.Database.GetPendingMigrationsAsync();
        MigrationStartupGuard.LogMigracionesAutomaticasDesactivadas(
            app.Logger, pendingMigrations.Count());
    }
}

// Seed: roles + usuario admin (fail-fast si ADMIN_INITIAL_* no están configurados)
await IdentitySeeder.SeedAsync(app.Services, app.Logger);

// Seed: catálogos del dominio catastral (usos de suelo, perfiles de importación, etc.)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
    await DomainSeeder.SeedAsync(db, logger);
    await DomainSeeder.SeedPerfilesImportacionAsync(db, logger);
}

// Inicializar bucket de MinIO al arrancar (crea si no existe)
using (var scope = app.Services.CreateScope())
{
    var minioSvc = scope.ServiceProvider.GetRequiredService<SG.Application.Abstractions.IMinioService>();
    await minioSvc.InicializarAsync();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Necesario para WebApplicationFactory<Program> en tests de integración.
public partial class Program { }
