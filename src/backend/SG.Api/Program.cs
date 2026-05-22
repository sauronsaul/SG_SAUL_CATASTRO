using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
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
    ["Minio:Endpoint"]        = Environment.GetEnvironmentVariable("MINIO_ENDPOINT"),
    ["Minio:AccessKey"]       = Environment.GetEnvironmentVariable("MINIO_ROOT_USER"),
    ["Minio:SecretKey"]       = Environment.GetEnvironmentVariable("MINIO_ROOT_PASSWORD"),
    ["Minio:UseSsl"]          = Environment.GetEnvironmentVariable("MINIO_USE_SSL"),
    ["Minio:BucketPredios"]   = Environment.GetEnvironmentVariable("MINIO_BUCKET_PREDIOS"),
};
builder.Configuration.AddInMemoryCollection(
    envMappings.Where(kv => kv.Value is not null).ToDictionary(kv => kv.Key, kv => kv.Value));

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

// Aplicar migraciones pendientes al arrancar — necesario en el primer deploy con Docker.
// En desarrollo (dotnet run) la base ya existe y MigrateAsync no hace nada si está al día.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}

// Seed: roles + usuario admin (fail-fast si ADMIN_INITIAL_* no están configurados)
await IdentitySeeder.SeedAsync(app.Services, app.Logger);

// Seed: catálogos del dominio catastral (usos de suelo, etc.)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();
    await DomainSeeder.SeedAsync(db, logger);
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
