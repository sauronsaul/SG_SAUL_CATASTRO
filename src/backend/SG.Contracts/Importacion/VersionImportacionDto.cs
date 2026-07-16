namespace SG.Contracts.Importacion;

public sealed record CrearVersionImportacionDto(
    Guid DatasetVersionId,
    string Estado);

public sealed record ReportePreliminarVersionDto(
    string? CapaEnCurso,
    IReadOnlyDictionary<string, int> CapasCompletadas,
    ValidacionPreviewVersionDto? Validacion = null);

public sealed record ValidacionPreviewVersionDto(
    DateTime GeneradoAtUtc,
    IReadOnlyList<BloqueantePreviewVersionDto> Bloqueantes,
    IReadOnlyList<GeometriasInvalidasCapaDto> GeometriasInvalidas,
    IReadOnlyList<ObservacionPreviewVersionDto> Observaciones,
    IReadOnlyList<DiferenciaConteoCapaDto> DiferenciasContraActiva,
    ProyeccionReconciliacionDto ProyeccionReconciliacion,
    EsquemaEvaluadoVersionDto? EsquemaEvaluado = null)
{
    public bool TieneBloqueantes => Bloqueantes.Count > 0;
}

public sealed record BloqueantePreviewVersionDto(
    string Codigo,
    string Mensaje,
    int Conteo,
    IReadOnlyList<string> Ejemplos);

public sealed record GeometriasInvalidasCapaDto(
    string Capa,
    int Conteo,
    IReadOnlyList<GeometriaInvalidaPreviewDto> Ejemplos,
    string Codigo = "O1");

public sealed record EsquemaEvaluadoVersionDto(
    string MunicipioCodigo,
    IReadOnlyList<CapaEsquemaEvaluadaDto> Capas);

public sealed record CapaEsquemaEvaluadaDto(
    string TipoCapa,
    string NombrePerfil,
    string NombreArchivoShp,
    string TablaDestino,
    bool Obligatoria);

public sealed record GeometriaInvalidaPreviewDto(
    int FilaOrigen,
    string Razon);

public sealed record ObservacionPreviewVersionDto(
    string Codigo,
    string Capa,
    string Mensaje,
    int Conteo,
    IReadOnlyList<ObservacionPreviewEjemploDto> Ejemplos);

public sealed record ObservacionPreviewEjemploDto(
    int FilaOrigen,
    IReadOnlyDictionary<string, string?> Identificadores);

public sealed record DiferenciaConteoCapaDto(
    string Capa,
    int ConteoVersion,
    int ConteoActiva,
    int DiferenciaAbsoluta,
    decimal? DiferenciaPorcentual);

public sealed record ProyeccionReconciliacionDto(
    int TotalMaestro,
    int AltasEstimadas,
    int AusenciasEstimadas,
    decimal PorcentajeCambio,
    decimal UmbralAdvertenciaPorcentaje,
    bool PosibleRenumeracion,
    bool Omitida = false,
    string? MotivoOmision = null);

public sealed record ResumenReconciliacionDto(
    int Altas,
    int Actualizadas,
    int SinCambio,
    int Ausencias,
    bool Omitida = false,
    string? MotivoOmision = null);

public sealed record ActivarVersionImportacionDto(
    Guid DatasetVersionId,
    string Estado,
    ResumenReconciliacionDto Resumen);

public sealed record EstadoVersionImportacionDto(
    Guid DatasetVersionId,
    int NumeroVersion,
    string MunicipioCodigo,
    string Estado,
    ReportePreliminarVersionDto ReportePreliminar,
    string? ErrorCarga,
    ResumenReconciliacionDto? ResumenReconciliacion = null);
