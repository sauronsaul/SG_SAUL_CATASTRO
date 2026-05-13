namespace SG.Infrastructure.Auditoria;

public sealed class AuditoriaEntidad
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; }
    public Guid? UsuarioId { get; set; }
    public string Modulo { get; set; } = string.Empty;
    public string Accion { get; set; } = string.Empty;
    public string EntidadTipo { get; set; } = string.Empty;
    public string EntidadId { get; set; } = string.Empty;
    public string? ValorAnterior { get; set; }
    public string? ValorNuevo { get; set; }
    public string Resultado { get; set; } = "OK";
    public string? IpOrigen { get; set; }
    public string? Motivo { get; set; }
}
