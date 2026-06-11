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
        "RowVersion", "xmin",
        "Token",   // refresh tokens nunca se loggean (ADR 0018)
        "Poligono" // NTS Polygon — excluido para evitar blobs GeoJSON en auditoría (ADR 0036)
    ];

    public AuditoriaInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    // ── Capa 1 — Guard append-only (ADR 0044) ──────────────────────────────
    private static void VerificarInmutabilidadAuditoria(DbContext ctx)
    {
        foreach (var entry in ctx.ChangeTracker
            .Entries<AuditoriaEntidad>()
            .Where(e => e.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new InvalidOperationException(
                $"Violación del invariante append-only: intento de {entry.State} " +
                $"sobre AuditoriaEntidad id={entry.Entity.Id}. " +
                $"Los registros de auditoría son inmutables (ADR 0041/0044).");
        }
    }

    // ── Lógica común de generación de registros ────────────────────────────
    private void GenerarRegistrosAuditoria(DbContext ctx)
    {
        var registros = new List<AuditoriaEntidad>();
        var now = DateTime.UtcNow;

        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.Entity is AuditoriaEntidad) continue;

            // OwnsOne entities se auditan como parte de su raíz agregada, nunca como
            // entradas independientes. El valor se fusiona en el registro del padre.
            if (entry.Metadata.IsOwned()) continue;

            // Un OwnsOne puede cambiar sin que cambien las columnas escalares del padre
            // (ej: técnico modifica solo la zona de UbicacionCatastral). En ese caso el
            // EntityEntry del padre queda Unchanged pero debe auditarse igual.
            bool ownedCambio = entry.References
                .Where(r => r.Metadata.TargetEntityType.IsOwned() && r.TargetEntry is not null)
                .Any(r => r.TargetEntry!.State
                    is EntityState.Modified or EntityState.Added or EntityState.Deleted);

            if (entry.State is not (EntityState.Added
                                 or EntityState.Modified
                                 or EntityState.Deleted)
                && !ownedCambio)
                continue;

            // Cuando solo cambió un owned, el padre tiene estado Unchanged → tratar como Update.
            var estadoEfectivo = entry.State == EntityState.Unchanged
                ? EntityState.Modified
                : entry.State;

            var accion = estadoEfectivo switch
            {
                EntityState.Added    => AccionAuditoria.Insert.ToString(),
                EntityState.Modified => AccionAuditoria.Update.ToString(),
                EntityState.Deleted  => AccionAuditoria.Delete.ToString(),
                _                    => string.Empty
            };

            var entidadId = entry.Properties
                .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                ?.CurrentValue?.ToString() ?? string.Empty;

            // Fusionar propiedades de los OwnsOne en el conjunto del padre.
            // Se excluye la PK del owned (shadow FK al padre) para evitar duplicar el Id.
            var propiedadesOwned = entry.References
                .Where(r => r.Metadata.TargetEntityType.IsOwned() && r.TargetEntry is not null)
                .SelectMany(r => r.TargetEntry!.Properties
                    .Where(p => !PropiedadesExcluidas.Contains(p.Metadata.Name)
                             && !p.Metadata.IsPrimaryKey()));

            var propsTodas = entry.Properties
                .Where(p => !PropiedadesExcluidas.Contains(p.Metadata.Name))
                .Concat(propiedadesOwned)
                .ToList();

            string? valorAnterior = null;
            string? valorNuevo = null;

            if (estadoEfectivo is EntityState.Modified or EntityState.Deleted)
            {
                var anterior = propsTodas.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue);
                valorAnterior = JsonSerializer.Serialize(anterior, JsonOptions);
            }

            if (estadoEfectivo is EntityState.Added or EntityState.Modified)
            {
                var nuevo = propsTodas.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
                valorNuevo = JsonSerializer.Serialize(nuevo, JsonOptions);
            }

            var schema = entry.Metadata.GetDefaultSchema()
                      ?? entry.Metadata.GetSchema()
                      ?? "dominio";

            registros.Add(new AuditoriaEntidad
            {
                Timestamp     = now,
                UsuarioId     = _currentUser.UserId,
                Modulo        = schema,
                Accion        = accion,
                EntidadTipo   = entry.Entity.GetType().Name,
                EntidadId     = entidadId,
                ValorAnterior = valorAnterior,
                ValorNuevo    = valorNuevo,
                IpOrigen      = _currentUser.IpOrigen,
                Resultado     = "OK"
            });
        }

        if (registros.Count > 0)
            ctx.Set<AuditoriaEntidad>().AddRange(registros);
    }

    // ── Override asíncrono (path normal) ───────────────────────────────────
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return base.SavingChangesAsync(eventData, result, cancellationToken);

        VerificarInmutabilidadAuditoria(eventData.Context);
        GenerarRegistrosAuditoria(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    // ── Override síncrono — cierra el gap si alguien llama SaveChanges() ───
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is null)
            return base.SavingChanges(eventData, result);

        VerificarInmutabilidadAuditoria(eventData.Context);
        GenerarRegistrosAuditoria(eventData.Context);

        return base.SavingChanges(eventData, result);
    }
}
