using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SG.Application.Abstractions;
using SG.Domain.Common;
using SG.Infrastructure.Identidad;

namespace SG.Infrastructure.Persistencia.Interceptors;

public sealed class AuditableEntitiesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public AuditableEntitiesInterceptor(ICurrentUserService currentUser)
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

        var now = DateTime.UtcNow;
        var userId = _currentUser.UserId;

        foreach (var entry in eventData.Context.ChangeTracker.Entries())
        {
            var isTracked = entry.Entity is AggregateRoot
                         || entry.Entity is UsuarioIdentidad;

            if (!isTracked) continue;

            if (entry.State == EntityState.Added)
            {
                entry.CurrentValues["CreatedAt"] = now;
                if (userId.HasValue)
                    entry.CurrentValues["CreatedBy"] = userId.Value;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.CurrentValues["UpdatedAt"] = now;
                if (userId.HasValue)
                    entry.CurrentValues["UpdatedBy"] = userId.Value;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
