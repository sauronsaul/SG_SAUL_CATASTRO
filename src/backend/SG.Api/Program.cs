using Serilog;
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
builder.Services.AddPersistencia(builder.Configuration);
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
