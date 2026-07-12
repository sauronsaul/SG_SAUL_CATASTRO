using SG.Domain.Common;

namespace SG.Domain.Importacion;

public sealed class DatasetVersion : AggregateRoot
{
    public int NumeroVersion { get; private set; }
    public string MunicipioCodigo { get; private set; } = string.Empty;
    public Guid? ImportacionId { get; private set; }
    public string OrigenDescripcion { get; private set; } = string.Empty;
    public EstadoDatasetVersion Estado { get; private set; }
    public DateTime? ActivadoAt { get; private set; }
    public Guid? ActivadoPor { get; private set; }
    public DateTime? ArchivadoAt { get; private set; }
    public Guid? ArchivadoPor { get; private set; }
    public string? RutaMinioPaquete { get; private set; }
    public string ReportePreliminar { get; private set; } = "{}";
    public string? ResumenReconciliacion { get; private set; }
    public string? ErrorCarga { get; private set; }

    private DatasetVersion() { }

    public static DatasetVersion Crear(
        int numeroVersion,
        string municipioCodigo,
        Guid? importacionId,
        string origenDescripcion,
        string? rutaMinioPaquete = null)
    {
        if (numeroVersion < 1)
            throw new DomainException("El número de versión debe ser mayor o igual a 1.");

        if (string.IsNullOrWhiteSpace(municipioCodigo))
            throw new DomainException("El código de municipio es requerido.");

        if (string.IsNullOrWhiteSpace(origenDescripcion))
            throw new DomainException("La descripción de origen es requerida.");

        return new DatasetVersion
        {
            NumeroVersion = numeroVersion,
            MunicipioCodigo = municipioCodigo.Trim(),
            ImportacionId = importacionId,
            OrigenDescripcion = origenDescripcion.Trim(),
            RutaMinioPaquete = string.IsNullOrWhiteSpace(rutaMinioPaquete)
                ? null
                : rutaMinioPaquete.Trim(),
            Estado = EstadoDatasetVersion.EnCarga,
        };
    }

    public void RegistrarProgreso(string reportePreliminar)
    {
        if (Estado != EstadoDatasetVersion.EnCarga)
            throw new DomainException("El progreso solo puede registrarse mientras DatasetVersion está EnCarga.");

        if (string.IsNullOrWhiteSpace(reportePreliminar))
            throw new DomainException("El reporte preliminar es requerido.");

        ReportePreliminar = reportePreliminar;
    }

    public void RegistrarErrorCarga(string errorCarga)
    {
        if (Estado != EstadoDatasetVersion.EnCarga)
            throw new DomainException("El error de carga solo puede registrarse mientras DatasetVersion está EnCarga.");

        if (string.IsNullOrWhiteSpace(errorCarga))
            throw new DomainException("El detalle del error de carga es requerido.");

        ErrorCarga = errorCarga.Trim();
    }

    public void RegistrarReportePreview(string reportePreliminar)
    {
        if (Estado != EstadoDatasetVersion.EnCarga)
            throw new DomainException("El reporte de preview debe registrarse antes de publicar PreviewListo.");

        if (string.IsNullOrWhiteSpace(reportePreliminar))
            throw new DomainException("El reporte de preview es requerido.");

        ReportePreliminar = reportePreliminar;
    }

    public void MarcarPreviewListo()
        => Transicionar(EstadoDatasetVersion.EnCarga, EstadoDatasetVersion.PreviewListo);

    public void Activar(Guid activadoPor)
    {
        Transicionar(EstadoDatasetVersion.PreviewListo, EstadoDatasetVersion.Activa);
        ActivadoAt = DateTime.UtcNow;
        ActivadoPor = activadoPor;
    }

    public void ReactivarDesdeArchivada(Guid activadoPor)
    {
        Transicionar(EstadoDatasetVersion.Archivada, EstadoDatasetVersion.Activa);
        ActivadoAt = DateTime.UtcNow;
        ActivadoPor = activadoPor;
        ArchivadoAt = null;
        ArchivadoPor = null;
    }

    public void Archivar(Guid archivadoPor)
    {
        Transicionar(EstadoDatasetVersion.Activa, EstadoDatasetVersion.Archivada);
        ArchivadoAt = DateTime.UtcNow;
        ArchivadoPor = archivadoPor;
    }

    public void MarcarFallida()
        => Transicionar(EstadoDatasetVersion.EnCarga, EstadoDatasetVersion.Fallida);

    public void Descartar()
        => Transicionar(EstadoDatasetVersion.PreviewListo, EstadoDatasetVersion.Descartada);

    public void RegistrarResumenReconciliacion(string resumenReconciliacion)
    {
        if (Estado != EstadoDatasetVersion.Activa)
            throw new DomainException("El resumen de reconciliación solo puede registrarse al activar la versión.");
        if (string.IsNullOrWhiteSpace(resumenReconciliacion))
            throw new DomainException("El resumen de reconciliación es requerido.");

        ResumenReconciliacion = resumenReconciliacion;
    }

    private void Transicionar(EstadoDatasetVersion origenPermitido, EstadoDatasetVersion destino)
    {
        if (Estado != origenPermitido)
        {
            throw new DomainException(
                $"No se puede transicionar DatasetVersion de {Estado} a {destino}. " +
                $"Solo permitido desde {origenPermitido}.");
        }

        Estado = destino;
    }
}
