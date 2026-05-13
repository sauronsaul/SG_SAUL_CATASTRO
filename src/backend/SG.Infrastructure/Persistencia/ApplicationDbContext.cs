using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SG.Infrastructure.Auditoria;
using SG.Infrastructure.Identidad;

namespace SG.Infrastructure.Persistencia;

public class ApplicationDbContext
    : IdentityDbContext<UsuarioIdentidad, RolIdentidad, Guid>
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditoriaEntidad> Auditorias => Set<AuditoriaEntidad>();

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasDefaultSchema("identidad");

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
