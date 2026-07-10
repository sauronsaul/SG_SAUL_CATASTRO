using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace SG.Api.Startup;

/// <summary>
/// Centraliza la decisión explícita de aplicar migraciones al arrancar la API.
/// </summary>
public static partial class MigrationStartupGuard
{
    public static bool DebeAplicarMigraciones(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    public static bool TienePassword(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return false;

        var builder = new DbConnectionStringBuilder
        {
            ConnectionString = connectionString,
        };

        return builder.TryGetValue("Password", out var password) &&
               !string.IsNullOrWhiteSpace(password?.ToString());
    }

    [LoggerMessage(
        EventId = 4801,
        Level = LogLevel.Information,
        Message = "Aplicando migraciones EF Core por SG_APPLY_MIGRATIONS=true.")]
    public static partial void LogAplicacionMigraciones(ILogger logger);

    [LoggerMessage(
        EventId = 4802,
        Level = LogLevel.Warning,
        Message = "Guard de migraciones activo: SG_APPLY_MIGRATIONS no es true. " +
                  "La API inicia sin migrar; migraciones pendientes: {CantidadMigracionesPendientes}.")]
    public static partial void LogMigracionesAutomaticasDesactivadas(
        ILogger logger, int cantidadMigracionesPendientes);
}
