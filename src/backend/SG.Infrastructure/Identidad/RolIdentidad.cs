using Microsoft.AspNetCore.Identity;

namespace SG.Infrastructure.Identidad;

public sealed class RolIdentidad : IdentityRole<Guid>
{
    public string? Descripcion { get; set; }
}
