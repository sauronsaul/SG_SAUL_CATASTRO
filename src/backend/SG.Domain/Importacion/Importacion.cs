using SG.Domain.Common;

namespace SG.Domain.Importacion;

public static class ImportacionErrores
{
    public static readonly DomainError NoEncontrada =
        new("Importacion.NoEncontrada", "La importación no fue encontrada.");

    public static readonly DomainError YaConfirmada =
        new("Importacion.YaConfirmada",
            "Esta importación ya fue confirmada y no puede procesarse de nuevo.");

    public static readonly DomainError EstadoInvalidoParaConfirmar =
        new("Importacion.EstadoInvalidoParaConfirmar",
            "Solo se puede confirmar una importación en estado PreviewGenerado.");
}

public sealed class Importacion : AggregateRoot
{
    public Guid PerfilId { get; private set; }
    public string NombreArchivo { get; private set; } = string.Empty;
    public string RutaMinioZip { get; private set; } = string.Empty;
    public DateTime FechaImportacion { get; private set; }
    public Guid ImportadoPorId { get; private set; }
    public int TotalFilas { get; private set; }

    // Conteos de preview — escritos SOLO en RegistrarConteosPreview
    public int FilasEstimadasACrear         { get; private set; }
    public int FilasEstimadasAActualizar    { get; private set; }
    public int FilasEstimadasAOmitir        { get; private set; }
    public int FilasEstimadasRechazadas     { get; private set; }
    public int FilasEstimadasConAdvertencia { get; private set; }

    // Conteos de confirmación — escritos SOLO en RegistrarConteosConfirmacion
    public int FilasCreadas         { get; private set; }
    public int FilasActualizadas    { get; private set; }
    public int FilasOmitidas        { get; private set; }
    public int FilasRechazadas      { get; private set; }
    public int FilasConAdvertencia  { get; private set; }

    public EstadoImportacion Estado { get; private set; }

    private Importacion() { }

    public static Importacion CrearPreview(
        Guid perfilId,
        string nombreArchivo,
        string rutaMinioZip,
        Guid importadoPorId,
        int totalFilas)
    {
        return new Importacion
        {
            PerfilId = perfilId,
            NombreArchivo = nombreArchivo.Trim(),
            RutaMinioZip = rutaMinioZip.Trim(),
            FechaImportacion = DateTime.UtcNow,
            ImportadoPorId = importadoPorId,
            TotalFilas = totalFilas,
            Estado = EstadoImportacion.PreviewGenerado,
        };
    }

    /// <summary>
    /// Registra los conteos proyectados del preview (qué pasaría si se confirma).
    /// Llamado desde GenerarPreviewImportacionHandler.
    /// </summary>
    public void RegistrarConteosPreview(
        int filasACrear,
        int filasAActualizar,
        int filasAOmitir,
        int filasRechazadas,
        int filasConAdvertencia)
    {
        if (Estado != EstadoImportacion.PreviewGenerado)
            throw new DomainException(
                $"No se pueden registrar conteos de preview en estado {Estado}. " +
                $"Solo permitido en PreviewGenerado.");

        FilasEstimadasACrear         = filasACrear;
        FilasEstimadasAActualizar    = filasAActualizar;
        FilasEstimadasAOmitir        = filasAOmitir;
        FilasEstimadasRechazadas     = filasRechazadas;
        FilasEstimadasConAdvertencia = filasConAdvertencia;
    }

    /// <summary>
    /// Registra los conteos reales producidos por la confirmación.
    /// Llamado desde ConfirmarImportacionHandler antes de Confirmar().
    /// </summary>
    public void RegistrarConteosConfirmacion(
        int filasCreadas,
        int filasActualizadas,
        int filasOmitidas,
        int filasRechazadas,
        int filasConAdvertencia)
    {
        if (Estado != EstadoImportacion.PreviewGenerado)
            throw new DomainException(
                $"No se pueden registrar conteos de confirmación en estado {Estado}. " +
                $"Solo permitido en PreviewGenerado (antes de transicionar a Confirmada).");

        FilasCreadas         = filasCreadas;
        FilasActualizadas    = filasActualizadas;
        FilasOmitidas        = filasOmitidas;
        FilasRechazadas      = filasRechazadas;
        FilasConAdvertencia  = filasConAdvertencia;
    }

    public void Confirmar()
    {
        if (Estado != EstadoImportacion.PreviewGenerado)
            throw new DomainException(
                $"No se puede confirmar una importación en estado {Estado}. " +
                $"Solo permitido desde PreviewGenerado.");

        Estado = EstadoImportacion.Confirmada;
    }

    public void MarcarFallida()
    {
        if (Estado != EstadoImportacion.PreviewGenerado)
            throw new DomainException(
                $"No se puede marcar como fallida una importación en estado {Estado}. " +
                $"Solo permitido desde PreviewGenerado.");

        Estado = EstadoImportacion.Fallida;
    }
}
