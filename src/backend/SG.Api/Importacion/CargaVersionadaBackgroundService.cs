using SG.Application.Abstractions.Importacion;
using SG.Application.Importacion.Versiones;

namespace SG.Api.Importacion;

public sealed partial class CargaVersionadaBackgroundService(
    IColaCargaVersionada cola,
    IServiceScopeFactory scopeFactory,
    ILogger<CargaVersionadaBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A3: la cola es solo memoria. Toda versión EnCarga existente al arrancar
        // corresponde a una carga interrumpida por reinicio y no puede reanudarse.
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var servicio = scope.ServiceProvider.GetRequiredService<ICargaVersionadaServicio>();
            await servicio.MarcarHuerfanasAlArrancarAsync(stoppingToken);
        }

        await foreach (var datasetVersionId in cola.LeerAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var servicio = scope.ServiceProvider.GetRequiredService<ICargaVersionadaServicio>();
                await servicio.CargarAsync(datasetVersionId, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogCargaFallida(logger, datasetVersionId, ex);
                await using var scope = scopeFactory.CreateAsyncScope();
                var servicio = scope.ServiceProvider.GetRequiredService<ICargaVersionadaServicio>();
                await servicio.MarcarFallidaYPurgarAsync(datasetVersionId, ex.Message, stoppingToken);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Falló la carga de DatasetVersion {DatasetVersionId}.")]
    private static partial void LogCargaFallida(ILogger logger, Guid datasetVersionId, Exception exception);
}
