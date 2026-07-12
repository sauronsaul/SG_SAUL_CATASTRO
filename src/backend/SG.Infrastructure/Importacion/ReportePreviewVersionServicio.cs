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
        var observaciones = await ObtenerObservacionesAsync(datasetVersionId, ct);
        var diferencias = await ObtenerDiferenciasContraActivaAsync(datasetVersionId, conteos, ct);
        var proyeccion = await ProyectarReconciliacionAsync(datasetVersionId, ct);

        var validacion = new ValidacionPreviewVersionDto(
            DateTime.UtcNow,
            bloqueantes,
            invalidas,
            observaciones,
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
                  AND geometria IS NOT NULL
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

    private async Task<IReadOnlyList<ObservacionPreviewVersionDto>> ObtenerObservacionesAsync(
        Guid datasetVersionId,
        CancellationToken ct)
    {
        var resultado = new List<ObservacionPreviewVersionDto>();

        var edificaciones = await db.CapasEdificaciones.AsNoTracking()
            .Where(x => x.DatasetVersionId == datasetVersionId && x.Geometria == null)
            .OrderBy(x => x.FilaOrigen)
            .Select(x => new
            {
                x.FilaOrigen,
                x.IdEdificacionOrigen,
                x.CodUv,
                x.CodMan,
                x.CodPred,
                x.CodigoBloque,
            })
            .ToListAsync(ct);
        AgregarO4(resultado, "capa_edificaciones", edificaciones.Count,
            edificaciones.Take(100).Select(x => new ObservacionPreviewEjemploDto(
                x.FilaOrigen,
                new Dictionary<string, string?>
                {
                    ["id_edificacion_origen"] = Formatear(x.IdEdificacionOrigen),
                    ["cod_uv"] = Formatear(x.CodUv),
                    ["cod_man"] = Formatear(x.CodMan),
                    ["cod_pred"] = Formatear(x.CodPred),
                    ["codigo_bloque"] = Formatear(x.CodigoBloque),
                })));

        var noFotografiados = await db.CapasPrediosNoFotografiados.AsNoTracking()
            .Where(x => x.DatasetVersionId == datasetVersionId && x.Geometria == null)
            .OrderBy(x => x.FilaOrigen)
            .Select(x => new
            {
                x.FilaOrigen,
                x.IdPredioOrigen,
                x.CodUv,
                x.CodMan,
                x.CodPred,
            })
            .ToListAsync(ct);
        AgregarO4(resultado, "capa_predios_no_fotografiados", noFotografiados.Count,
            noFotografiados.Take(100).Select(x => new ObservacionPreviewEjemploDto(
                x.FilaOrigen,
                new Dictionary<string, string?>
                {
                    ["id_predio_origen"] = Formatear(x.IdPredioOrigen),
                    ["cod_uv"] = Formatear(x.CodUv),
                    ["cod_man"] = Formatear(x.CodMan),
                    ["cod_pred"] = Formatear(x.CodPred),
                })));

        var manzanas = await db.CapasManzanas.AsNoTracking()
            .Where(x => x.DatasetVersionId == datasetVersionId && x.Geometria == null)
            .OrderBy(x => x.FilaOrigen)
            .Select(x => new { x.FilaOrigen, x.CodigoGeografico, x.CodUv, x.CodMan })
            .ToListAsync(ct);
        AgregarO4(resultado, "capa_manzanas", manzanas.Count,
            manzanas.Take(100).Select(x => new ObservacionPreviewEjemploDto(
                x.FilaOrigen,
                new Dictionary<string, string?>
                {
                    ["codigo_geografico"] = x.CodigoGeografico,
                    ["cod_uv"] = Formatear(x.CodUv),
                    ["cod_man"] = Formatear(x.CodMan),
                })));

        var distritos = await db.CapasDistritos.AsNoTracking()
            .Where(x => x.DatasetVersionId == datasetVersionId && x.Geometria == null)
            .OrderBy(x => x.FilaOrigen)
            .Select(x => new { x.FilaOrigen, x.CodigoGeografico, x.CodUv, x.Nombre })
            .ToListAsync(ct);
        AgregarO4(resultado, "capa_distritos", distritos.Count,
            distritos.Take(100).Select(x => new ObservacionPreviewEjemploDto(
                x.FilaOrigen,
                new Dictionary<string, string?>
                {
                    ["codigo_geografico"] = x.CodigoGeografico,
                    ["cod_uv"] = Formatear(x.CodUv),
                    ["nombre"] = x.Nombre,
                })));

        var zonas = await db.CapasZonas.AsNoTracking()
            .Where(x => x.DatasetVersionId == datasetVersionId && x.Geometria == null)
            .OrderBy(x => x.FilaOrigen)
            .Select(x => new { x.FilaOrigen, x.IdZonaOrigen, x.CodigoGeografico, x.NombreZona })
            .ToListAsync(ct);
        AgregarO4(resultado, "capa_zonas", zonas.Count,
            zonas.Take(100).Select(x => new ObservacionPreviewEjemploDto(
                x.FilaOrigen,
                new Dictionary<string, string?>
                {
                    ["id_zona_origen"] = Formatear(x.IdZonaOrigen),
                    ["codigo_geografico"] = x.CodigoGeografico,
                    ["nombre_zona"] = x.NombreZona,
                })));

        var vias = await db.CapasVias.AsNoTracking()
            .Where(x => x.DatasetVersionId == datasetVersionId && x.Geometria == null)
            .OrderBy(x => x.FilaOrigen)
            .Select(x => new { x.FilaOrigen, x.Nombre, x.Material, x.Tipo })
            .ToListAsync(ct);
        AgregarO4(resultado, "capa_vias", vias.Count,
            vias.Take(100).Select(x => new ObservacionPreviewEjemploDto(
                x.FilaOrigen,
                new Dictionary<string, string?>
                {
                    ["nombre"] = x.Nombre,
                    ["material"] = x.Material,
                    ["tipo"] = x.Tipo,
                })));

        return resultado;
    }

    private static void AgregarO4(
        List<ObservacionPreviewVersionDto> resultado,
        string capa,
        int conteo,
        IEnumerable<ObservacionPreviewEjemploDto> ejemplos)
    {
        if (conteo == 0)
            return;

        resultado.Add(new ObservacionPreviewVersionDto(
            "O4",
            capa,
            "La capa contiene geometrías nulas; las filas se conservaron con sus atributos para revisión del GAM.",
            conteo,
            ejemplos.ToList()));
    }

    private static string? Formatear(object? valor) => valor switch
    {
        null => null,
        IFormattable formateable => formateable.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString(),
    };

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
