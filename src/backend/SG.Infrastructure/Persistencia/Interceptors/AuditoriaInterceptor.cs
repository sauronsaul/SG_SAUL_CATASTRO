using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SG.Application.Abstractions;
using SG.Infrastructure.Auditoria;

namespace SG.Infrastructure.Persistencia.Interceptors;

public sealed class AuditoriaInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles,
        WriteIndented = false
    };

    private static readonly HashSet<string> PropiedadesExcluidas =
    [
        "PasswordHash", "SecurityStamp", "ConcurrencyStamp",
        "RowVersion", "xmin"
    ];

    public AuditoriaInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        var registros = new List<AuditoriaEntidad>();
        var now = DateTime.UtcNow;

        foreach (var entry in eventData.Context.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditoriaEntidad) continue;

            if (entry.State is not (EntityState.Added
                                 or EntityState.Modified
                                 or EntityState.Deleted))
                continue;

            var accion = entry.State switch
            {
                EntityState.Added    => AccionAuditoria.Insert.ToString(),
                EntityState.Modified => AccionAuditoria.Update.ToString(),
                EntityState.Deleted  => AccionAuditoria.Delete.ToString(),
                _                    => string.Empty
            };

            var entidadId = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                ?.CurrentValue?.ToString() ?? string.Empty;

            string? valorAnterior = null;
            string? valorNuevo = null;

            if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
            {
                var anterior = entry.Properties
                    .Where(p => !PropiedadesExcluidas.Contains(p.Metadata.Name))
                    .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                valorAnterior = JsonSerializer.Serialize(anterior, JsonOptions);
            }

            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                var nuevo = entry.Properties
                    .Where(p => !PropiedadesExcluidas.Contains(p.Metadata.Name))
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                valorNuevo = JsonSerializer.Serialize(nuevo, JsonOptions);
            }

            var schema = entry.Metadata.GetDefaultSchema()
                      ?? entry.Metadata.GetSchema()
                      ?? "dominio";

            registros.Add(new AuditoriaEntidad
            {
                Timestamp    = now,
                UsuarioId    = _currentUser.UserId,
                Modulo       = schema,
                Accion       = accion,
                EntidadTipo  = entry.Entity.GetType().Name,
                EntidadId    = entidadId,
                ValorAnterior = valorAnterior,
                ValorNuevo   = valorNuevo,
                IpOrigen     = _currentUser.IpOrigen,
                Resultado    = "OK"
            });
        }

        if (registros.Count > 0)
            eventData.Context.Set<AuditoriaEntidad>().AddRange(registros);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
