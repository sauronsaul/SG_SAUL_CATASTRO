using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SG.Web;
using SG.Web.Models;
using SG.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress),
});
builder.Services.AddScoped<AutenticacionService>();
builder.Services.AddScoped<PrediosService>();
builder.Services.AddScoped<SesionAutenticacion>();
builder.Services.AddScoped<EstadoVisor>();
builder.Services.AddSingleton(CargarConfiguracionVisor(builder.Configuration));

await builder.Build().RunAsync();

static ConfiguracionVisor CargarConfiguracionVisor(IConfiguration configuration)
{
    var municipio = configuration["Visor:MunicipioCodigo"];
    if (string.IsNullOrWhiteSpace(municipio))
        throw new InvalidOperationException("Visor:MunicipioCodigo no esta configurado.");

    var limites = configuration.GetSection("Visor:Mapa:Limites")
        .GetChildren()
        .Select(x => double.Parse(
            x.Value ?? throw new InvalidOperationException("Visor:Mapa:Limites contiene un valor nulo."),
            CultureInfo.InvariantCulture))
        .ToArray();

    if (limites.Length != 4)
        throw new InvalidOperationException("Visor:Mapa:Limites debe contener [oeste, sur, este, norte].");

    return new ConfiguracionVisor(municipio, limites);
}
