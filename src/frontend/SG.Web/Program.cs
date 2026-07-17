using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SG.Web;
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
builder.Services.AddScoped<VisorService>();
builder.Services.AddScoped<SesionAutenticacion>();
builder.Services.AddScoped<EstadoVisor>();
await builder.Build().RunAsync();
