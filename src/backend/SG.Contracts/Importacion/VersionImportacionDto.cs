namespace SG.Contracts.Importacion;

public sealed record CrearVersionImportacionDto(
    Guid DatasetVersionId,
    string Estado);

public sealed record ReportePreliminarVersionDto(
    string? CapaEnCurso,
    IReadOnlyDictionary<string, int> CapasCompletadas);

public sealed record EstadoVersionImportacionDto(
    Guid DatasetVersionId,
    int NumeroVersion,
    string MunicipioCodigo,
    string Estado,
    ReportePreliminarVersionDto ReportePreliminar,
    string? ErrorCarga);
