using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public static class VersionImportacionErrores
{
    public static readonly DomainError PaqueteInvalido =
        new("VersionImportacion.PaqueteInvalido", "El paquete ZIP de las siete capas es inválido.");
    public static readonly DomainError NoEncontrada =
        new("VersionImportacion.NoEncontrada", "La versión de dataset no fue encontrada.");
    public static readonly DomainError UsuarioNoDisponible =
        new("VersionImportacion.UsuarioNoDisponible", "No se pudo identificar al usuario que activa la versión.");

    public static readonly DomainError EstadoNoActivable =
        new("VersionImportacion.EstadoNoActivable", "La versión debe estar en PreviewListo o Archivada para activarse.");

    public static readonly DomainError ReporteNoDisponible =
        new("VersionImportacion.ReporteNoDisponible", "La versión no tiene un reporte de preview completo.");

    public static DomainError ReporteConBloqueantes(IEnumerable<string> codigos) =>
        new("VersionImportacion.ReporteConBloqueantes",
            $"La versión no puede activarse porque contiene bloqueantes: {string.Join(", ", codigos)}.");

    public static DomainError ReconciliacionInvalida(string detalle) =>
        new("VersionImportacion.ReconciliacionInvalida", $"La reconciliación no pudo completarse: {detalle}");
}
