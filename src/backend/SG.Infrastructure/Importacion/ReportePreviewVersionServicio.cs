using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Importacion;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Importacion;

internal sealed class ReportePreviewVersionServicio(
    ApplicationDbContext db,
    IConfiguration configuration) : IReportePreviewVersionServicio
{
    private const decimal UmbralPredeterminado = 10m;

    public async Task<ReportePreliminarVersionDto> GenerarAsync(
        Guid datasetVersionId,
        CancellationToken ct = default)
    {
        var conteos = await ContarCapasAsync(datasetVersionId, ct);
        var bloqueantes = await ObtenerBloqueantesAsync(datasetVersionId, conteos, ct);
        var invalidas = await ObtenerGeometriasInvalidasAsync(datasetVersionId, ct);
        var diferencias = await ObtenerDiferenciasContraActivaAsync(datasetVersionId, conteos, ct);
        var proyeccion = await ProyectarReconciliacionAsync(datasetVersionId, ct);

        var validacion = new ValidacionPreviewVersionDto(
            DateTime.UtcNow,
            bloqueantes,
            invalidas,
            diferencias,
            proyeccion);
        return new ReportePreliminarVersionDto(null, conteos, validacion);
    }

    private async Task<IReadOnlyList<BloqueantePreviewVersionDto>> ObtenerBloqueantesAsync(
        Guid datasetVersionId,
        IReadOnlyDictionary<string, int> conteos,
        CancellationToken ct)
    {
        var resultado = new List<BloqueantePreviewVersionDto>();
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

        var capasConFilas = conteos.Count(x => x.Value > 0);
        if (capasConFilas < DefinicionesCapasVersionadasUyuni.Todas.Count)
        {
            resultado.Add(new BloqueantePreviewVersionDto(
                "B3",
                "La versión no contiene filas en las siete capas requeridas.",
                DefinicionesCapasVersionadasUyuni.Todas.Count - capasConFilas,
                conteos.Where(x => x.Value == 0).Select(x => x.Key).ToList()));
        }

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

        return resultado;
    }

    private async Task<IReadOnlyList<GeometriasInvalidasCapaDto>> ObtenerGeometriasInvalidasAsync(
        Guid datasetVersionId,
        CancellationToken ct)
    {
        var resultado = new List<GeometriasInvalidasCapaDto>();
        foreach (var definicion in DefinicionesCapasVersionadasUyuni.Todas)
        {
            var sql = """
                SELECT fila_origen AS fila_origen,
                       ST_IsValidReason(geometria) AS razon,
                       COUNT(*) OVER()::int AS total
                FROM dominio.__TABLA__
                WHERE dataset_version_id = {0}
                  AND NOT ST_IsValid(geometria)
                ORDER BY fila_origen
                LIMIT 100
                """.Replace("__TABLA__", definicion.NombreTabla, StringComparison.Ordinal);
            var filas = await db.Database
                .SqlQueryRaw<GeometriaInvalidaSql>(sql, datasetVersionId)
                .ToListAsync(ct);
            if (filas.Count == 0)
                continue;

            resultado.Add(new GeometriasInvalidasCapaDto(
                definicion.NombreTabla,
                filas[0].Total,
                filas.Select(x => new GeometriaInvalidaPreviewDto(x.FilaOrigen, x.Razon)).ToList()));
        }

        return resultado;
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
        var parcelas = db.CapasParcelas.AsNoTracking()
            .Where(x => x.DatasetVersionId == datasetVersionId);
        var totalMaestro = await db.Predios.AsNoTracking().CountAsync(ct);
        var altas = await parcelas.CountAsync(capa => !db.Predios.Any(predio =>
            predio.CodUv == capa.CodUv &&
            predio.CodMan == capa.CodMan &&
            predio.CodPred == capa.CodPred), ct);
        var ausencias = await db.Predios.AsNoTracking().CountAsync(predio => !parcelas.Any(capa =>
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
        CancellationToken ct) => new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["capa_parcelas"] = await db.CapasParcelas.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
        ["capa_edificaciones"] = await db.CapasEdificaciones.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
        ["capa_predios_no_fotografiados"] = await db.CapasPrediosNoFotografiados.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
        ["capa_manzanas"] = await db.CapasManzanas.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
        ["capa_distritos"] = await db.CapasDistritos.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
        ["capa_zonas"] = await db.CapasZonas.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
        ["capa_vias"] = await db.CapasVias.AsNoTracking().CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
    };

    private sealed class GeometriaInvalidaSql
    {
        public int FilaOrigen { get; init; }
        public string Razon { get; init; } = string.Empty;
        public int Total { get; init; }
    }
}
