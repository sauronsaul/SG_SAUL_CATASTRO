using SG.Application.Abstractions.Autenticacion;
using SG.Infrastructure.Auditoria;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Seguridad;

internal sealed class AuditoriaService(ApplicationDbContext db) : IAuditoriaService
{
    public async Task RegistrarAsync(
        string modulo,
        string accion,
        Guid? usuarioId,
        string entidadTipo,
        string entidadId,
        string resultado,
        string? ipOrigen,
        string? motivo = null,
        CancellationToken ct = default)
    {
        var registro = new AuditoriaEntidad
        {
            Timestamp = DateTime.UtcNow,
            UsuarioId = usuarioId,
            Modulo = modulo,
            Accion = accion,
            EntidadTipo = entidadTipo,
            EntidadId = entidadId,
            Resultado = resultado,
            IpOrigen = ipOrigen,
            Motivo = motivo,
        };

        db.Auditorias.Add(registro);
        await db.SaveChangesAsync(ct);
    }
}
