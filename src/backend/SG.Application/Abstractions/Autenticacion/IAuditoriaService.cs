namespace SG.Application.Abstractions.Autenticacion;

public interface IAuditoriaService
{
    Task RegistrarAsync(
        string modulo,
        string accion,
        Guid? usuarioId,
        string entidadTipo,
        string entidadId,
        string resultado,
        string? ipOrigen,
        string? motivo = null,
        CancellationToken ct = default);
}
