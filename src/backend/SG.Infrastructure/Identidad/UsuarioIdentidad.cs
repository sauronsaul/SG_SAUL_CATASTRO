using Microsoft.AspNetCore.Identity;

namespace SG.Infrastructure.Identidad;

public sealed class UsuarioIdentidad : IdentityUser<Guid>
{
    public string NombreCompleto { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public uint RowVersion { get; set; }
}
