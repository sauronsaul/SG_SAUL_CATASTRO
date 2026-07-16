using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public static class VersionImportacionErrores
{
    public static DomainError PaqueteInvalido(string detalle) =>
        new("VersionImportacion.PaqueteInvalido", $"El paquete ZIP es inválido: {detalle}");

    public static DomainError MunicipioNoEncontrado(string codigoIne) =>
        new("VersionImportacion.MunicipioNoEncontrado", $"El municipio INE {codigoIne} no existe en el catálogo.");

    public static DomainError EsquemaMunicipalNoConfigurado(string codigoIne) =>
        new("VersionImportacion.EsquemaMunicipalNoConfigurado", $"El municipio INE {codigoIne} no tiene un esquema de capas configurado.");

    public static DomainError EsquemaMunicipalInconsistente(string detalle) =>
        new("VersionImportacion.EsquemaMunicipalInconsistente", $"El esquema municipal es inconsistente: {detalle}");
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
