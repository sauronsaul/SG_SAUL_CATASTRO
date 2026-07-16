using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Importacion;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Importacion;

internal sealed class ReportePreviewVersionServicio(
    ApplicationDbContext db,
    IEsquemaCapasMunicipioRepositorio esquemas,
    IConfiguration configuration) : IReportePreviewVersionServicio
{
    private const decimal UmbralPredeterminado = 10m;

    public async Task<ReportePreliminarVersionDto> GenerarAsync(
        Guid datasetVersionId,
        CancellationToken ct = default)
    {
        var version = await db.DatasetVersiones.AsNoTracking()
            .SingleAsync(x => x.Id == datasetVersionId, ct);
        var esquemaMunicipal = await esquemas.ListarAsync(version.MunicipioCodigo, ct);
        var tienePredios = esquemaMunicipal.Any(x => x.TipoCapa == TipoCapa.Predios);
        var conteos = await ContarCapasAsync(datasetVersionId, ct);
        var bloqueantes = await ObtenerBloqueantesAsync(datasetVersionId, conteos, esquemaMunicipal, ct);
        var invalidas = await ObtenerGeometriasInvalidasAsync(datasetVersionId, esquemaMunicipal, ct);
        var observaciones = await ObtenerObservacionesAsync(datasetVersionId, esquemaMunicipal, ct);
        var diferencias = await ObtenerDiferenciasContraActivaAsync(datasetVersionId, conteos, ct);
        var proyeccion = tienePredios
            ? await ProyectarReconciliacionAsync(datasetVersionId, ct)
            : new ProyeccionReconciliacionDto(
                0, 0, 0, 0m,
                configuration.GetValue<decimal?>("Importacion:UmbralRenumeracionPorcentaje") ?? UmbralPredeterminado,
                false,
                true,
                "Esquema municipal sin capa de predios.");
        var esquemaEvaluado = new EsquemaEvaluadoVersionDto(
            version.MunicipioCodigo,
            esquemaMunicipal.Select(x => new CapaEsquemaEvaluadaDto(
                x.TipoCapa.ToString(),
                x.NombrePerfil,
                x.NombreArchivoShp,
                x.TablaDestino,
                x.Obligatoria)).ToList());

        var validacion = new ValidacionPreviewVersionDto(
            DateTime.UtcNow,
            bloqueantes,
            invalidas,
            observaciones,
            diferencias,
            proyeccion,
            esquemaEvaluado);
        return new ReportePreliminarVersionDto(null, conteos, validacion);
    }

    private async Task<IReadOnlyList<BloqueantePreviewVersionDto>> ObtenerBloqueantesAsync(
        Guid datasetVersionId,
        IReadOnlyDictionary<string, int> conteos,
        IReadOnlyList<EsquemaCapaMunicipio> esquemaMunicipal,
        CancellationToken ct)
    {
        var resultado = new List<BloqueantePreviewVersionDto>();
        var tienePredios = esquemaMunicipal.Any(x => x.TipoCapa == TipoCapa.Predios);
        if (tienePredios)
        {
            var duplicados = await db.CapasParcelas
                .AsNoTracking()
                .Where(x => x.DatasetVersionId == datasetVersionId)
                .GroupBy(x => new { x.CodUv, x.CodMan, x.CodPred })
                .Where(x => x.Count() > 1)
                .Select(x => new { x.Key.CodUv, x.Key.CodMan, x.Key.CodPred, Conteo = x.Count() })
                .ToListAsync(ct);
            if (duplicados.Count > 0)
            {
                resultado.Add(new BloqueantePreviewVersionDto(
                    "B1",
                    "La capa de parcelas contiene tripletes duplicados.",
                    duplicados.Count,
                    duplicados.Take(100)
                        .Select(x => $"{x.CodUv}-{x.CodMan}-{x.CodPred} ({x.Conteo} filas)")
                        .ToList()));
            }

            var componentesNulos = await db.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value"
                FROM dominio.capa_parcelas
                WHERE dataset_version_id = {datasetVersionId}
                  AND (cod_uv IS NULL OR cod_man IS NULL OR cod_pred IS NULL)
                """).SingleAsync(ct);
            if (componentesNulos > 0)
            {
                resultado.Add(new BloqueantePreviewVersionDto(
                    "B2",
                    "Existen parcelas con componentes nulos en el triplete catastral.",
                    componentesNulos,
                    []));
            }
        }

        var obligatorias = esquemaMunicipal.Where(x => x.Obligatoria).ToList();
        var faltantes = obligatorias.Where(x => !conteos.TryGetValue(x.TablaDestino, out var filas) || filas == 0).ToList();
        if (faltantes.Count > 0)
        {
            resultado.Add(new BloqueantePreviewVersionDto(
                "B3",
                "La versión no contiene filas en todas las capas obligatorias del municipio.",
                faltantes.Count,
                faltantes.Select(x => $"{x.TipoCapa} ({x.TablaDestino})").ToList()));
        }

        if (tienePredios)
        {
            var superficiesInvalidas = await db.CapasParcelas
                .AsNoTracking()
                .Where(x => x.DatasetVersionId == datasetVersionId &&
                            (x.Superficie == null || x.Superficie <= 0))
                .Select(x => x.FilaOrigen)
                .ToListAsync(ct);
            if (superficiesInvalidas.Count > 0)
            {
                resultado.Add(new BloqueantePreviewVersionDto(
                    "B4",
                    "Existen parcelas con superficie declarada nula o no positiva.",
                    superficiesInvalidas.Count,
                    superficiesInvalidas.Take(100)
                        .Select(x => x.ToString(CultureInfo.InvariantCulture))
                        .ToList()));
            }
        }

        return resultado;
    }

    private async Task<IReadOnlyList<GeometriasInvalidasCapaDto>> ObtenerGeometriasInvalidasAsync(
        Guid datasetVersionId,
        IReadOnlyList<EsquemaCapaMunicipio> esquemaMunicipal,
        CancellationToken ct)
    {
        var resultado = new List<GeometriasInvalidasCapaDto>();
        foreach (var definicion in esquemaMunicipal)
        {
            var sql = """
                SELECT fila_origen AS fila_origen,
                       ST_IsValidReason(geometria) AS razon,
                       COUNT(*) OVER()::int AS total
                FROM dominio.__TABLA__
                WHERE dataset_version_id = {0}
                  AND geometria IS NOT NULL
                  AND NOT ST_IsValid(geometria)
                ORDER BY fila_origen
                LIMIT 100
                """.Replace("__TABLA__", definicion.TablaDestino, StringComparison.Ordinal);
            var filas = await db.Database
                .SqlQueryRaw<GeometriaInvalidaSql>(sql, datasetVersionId)
                .ToListAsync(ct);
            if (filas.Count == 0)
                continue;

            resultado.Add(new GeometriasInvalidasCapaDto(
                definicion.TablaDestino,
                filas[0].Total,
                filas.Select(x => new GeometriaInvalidaPreviewDto(x.FilaOrigen, x.Razon)).ToList()));
        }

        return resultado;
    }

    private async Task<IReadOnlyList<ObservacionPreviewVersionDto>> ObtenerObservacionesAsync(
        Guid datasetVersionId,
        IReadOnlyList<EsquemaCapaMunicipio> esquemaMunicipal,
        CancellationToken ct)
    {
        var resultado = new List<ObservacionPreviewVersionDto>();

        foreach (var definicion in esquemaMunicipal)
        {
            var sql = """
                SELECT fila_origen AS fila_origen,
                       ((to_jsonb(c) - 'id' - 'dataset_version_id' - 'geometria' - 'atributos_extra')
                         || atributos_extra)::text AS identificadores,
                       COUNT(*) OVER()::int AS total
                FROM dominio.__TABLA__ c
                WHERE dataset_version_id = {0}
                  AND geometria IS NULL
                ORDER BY fila_origen
                LIMIT 100
                """.Replace("__TABLA__", definicion.TablaDestino, StringComparison.Ordinal);
            var filas = await db.Database
                .SqlQueryRaw<GeometriaNulaSql>(sql, datasetVersionId)
                .ToListAsync(ct);
            if (filas.Count == 0)
                continue;

            resultado.Add(new ObservacionPreviewVersionDto(
                "O4",
                definicion.TablaDestino,
                "La capa contiene geometrías nulas; las filas se conservaron con sus atributos para revisión del GAM.",
                filas[0].Total,
                filas.Select(x => new ObservacionPreviewEjemploDto(
                    x.FilaOrigen,
                    DeserializarIdentificadores(x.Identificadores))).ToList()));
        }

        return resultado;
    }

    private static Dictionary<string, string?> DeserializarIdentificadores(string json)
    {
        using var documento = JsonDocument.Parse(json);
        return documento.RootElement.EnumerateObject().ToDictionary(
            x => x.Name,
            x => x.Value.ValueKind == JsonValueKind.Null ? null : x.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<DiferenciaConteoCapaDto>> ObtenerDiferenciasContraActivaAsync(
        Guid datasetVersionId,
        IReadOnlyDictionary<string, int> conteos,
        CancellationToken ct)
    {
        var version = await db.DatasetVersiones.AsNoTracking()
            .SingleAsync(x => x.Id == datasetVersionId, ct);
        var activa = await db.DatasetVersiones.AsNoTracking()
            .Where(x => x.MunicipioCodigo == version.MunicipioCodigo &&
                        x.Estado == EstadoDatasetVersion.Activa &&
                        x.Id != datasetVersionId)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(ct);
        if (activa is null)
            return [];

        var conteosActiva = await ContarCapasAsync(activa.Value, ct);
        return conteos.Select(x =>
        {
            var anterior = conteosActiva[x.Key];
            var diferencia = x.Value - anterior;
            return new DiferenciaConteoCapaDto(
                x.Key,
                x.Value,
                anterior,
                Math.Abs(diferencia),
                anterior == 0 ? null : Math.Round(diferencia * 100m / anterior, 2));
        }).ToList();
    }

    private async Task<ProyeccionReconciliacionDto> ProyectarReconciliacionAsync(
        Guid datasetVersionId,
        CancellationToken ct)
    {
        var municipioCodigo = await db.DatasetVersiones.AsNoTracking()
            .Where(x => x.Id == datasetVersionId)
            .Select(x => x.MunicipioCodigo)
            .SingleAsync(ct);
        var parcelas = db.CapasParcelas.AsNoTracking()
            .Where(x => x.DatasetVersionId == datasetVersionId);
        var maestro = db.Predios.AsNoTracking()
            .Where(x => x.MunicipioCodigo == municipioCodigo);
        var totalMaestro = await maestro.CountAsync(ct);
        var altas = await parcelas.CountAsync(capa => !maestro.Any(predio =>
            predio.CodUv == capa.CodUv &&
            predio.CodMan == capa.CodMan &&
            predio.CodPred == capa.CodPred), ct);
        var ausencias = await maestro.CountAsync(predio => !parcelas.Any(capa =>
            capa.CodUv == predio.CodUv &&
            capa.CodMan == predio.CodMan &&
            capa.CodPred == predio.CodPred), ct);

        var umbral = configuration.GetValue<decimal?>(
            "Importacion:UmbralRenumeracionPorcentaje") ?? UmbralPredeterminado;
        var porcentaje = totalMaestro == 0
            ? 0m
            : Math.Round((altas + ausencias) * 100m / totalMaestro, 2);
        return new ProyeccionReconciliacionDto(
            totalMaestro,
            altas,
            ausencias,
            porcentaje,
            umbral,
            totalMaestro > 0 && porcentaje > umbral);
    }

    private async Task<IReadOnlyDictionary<string, int>> ContarCapasAsync(
        Guid datasetVersionId,
        CancellationToken ct)
    {
        var version = await db.DatasetVersiones.AsNoTracking().SingleAsync(x => x.Id == datasetVersionId, ct);
        var esquemaMunicipal = await esquemas.ListarAsync(version.MunicipioCodigo, ct);
        var resultado = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var definicion in esquemaMunicipal)
        {
            resultado[definicion.TablaDestino] = definicion.TipoCapa switch
            {
                TipoCapa.Predios => await db.CapasParcelas.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
                TipoCapa.Construcciones => await db.CapasEdificaciones.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
                TipoCapa.PrediosNoFotografiados => await db.CapasPrediosNoFotografiados.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
                TipoCapa.Manzanas => await db.CapasManzanas.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
                TipoCapa.Distritos => await db.CapasDistritos.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
                TipoCapa.ZonasValuacion => await db.CapasZonas.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
                TipoCapa.Vias => await db.CapasVias.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
                TipoCapa.AreasUrbanas => await db.CapasAreasUrbanas.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
                TipoCapa.PuntosGeodesicos => await db.CapasPuntosGeodesicos.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
                _ => throw new InvalidOperationException($"Tipo de capa no soportado: {definicion.TipoCapa}."),
            };
        }
        return resultado;
    }

    private sealed class GeometriaInvalidaSql
    {
        public int FilaOrigen { get; init; }
        public string Razon { get; init; } = string.Empty;
        public int Total { get; init; }
    }

    private sealed class GeometriaNulaSql
    {
        public int FilaOrigen { get; init; }
        public string Identificadores { get; init; } = "{}";
        public int Total { get; init; }
    }
}
