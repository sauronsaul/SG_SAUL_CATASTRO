using System.Data;
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SG.Application.Abstractions.Importacion;
using SG.Application.Importacion.Versiones;
using SG.Contracts.Importacion;
using SG.Domain.Catastro;
using SG.Domain.Catastro.ValueObjects;
using SG.Domain.Common;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Importacion;

internal sealed partial class ActivacionVersionServicio(
    ApplicationDbContext db,
    IEsquemaCapasMunicipioRepositorio esquemas,
    ILogger<ActivacionVersionServicio> logger)
    : IActivacionVersionServicio
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<ActivarVersionImportacionDto>> ActivarAsync(
        Guid datasetVersionId,
        Guid usuarioId,
        CancellationToken ct = default)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct);

        try
        {
            var version = await db.DatasetVersiones
                .SingleOrDefaultAsync(x => x.Id == datasetVersionId, ct);
            if (version is null)
            {
                await transaccion.RollbackAsync(ct);
                return Result.Failure<ActivarVersionImportacionDto>(VersionImportacionErrores.NoEncontrada);
            }

            if (version.Estado is not (EstadoDatasetVersion.PreviewListo or EstadoDatasetVersion.Archivada))
            {
                await transaccion.RollbackAsync(ct);
                return Result.Failure<ActivarVersionImportacionDto>(VersionImportacionErrores.EstadoNoActivable);
            }

            var reporte = DeserializarReporte(version.ReportePreliminar);
            if (reporte?.Validacion is null)
            {
                await transaccion.RollbackAsync(ct);
                return Result.Failure<ActivarVersionImportacionDto>(VersionImportacionErrores.ReporteNoDisponible);
            }

            if (reporte.Validacion.TieneBloqueantes)
            {
                await transaccion.RollbackAsync(ct);
                return Result.Failure<ActivarVersionImportacionDto>(
                    VersionImportacionErrores.ReporteConBloqueantes(
                        reporte.Validacion.Bloqueantes.Select(x => x.Codigo)));
            }

            var esquemaMunicipal = await esquemas.ListarAsync(version.MunicipioCodigo, ct);
            if (esquemaMunicipal.Count == 0)
            {
                await transaccion.RollbackAsync(ct);
                return Result.Failure<ActivarVersionImportacionDto>(
                    VersionImportacionErrores.EsquemaMunicipalNoConfigurado(version.MunicipioCodigo));
            }
            var tienePredios = esquemaMunicipal.Any(x => x.TipoCapa == TipoCapa.Predios);

            var activaActual = await db.DatasetVersiones
                .SingleOrDefaultAsync(x =>
                    x.MunicipioCodigo == version.MunicipioCodigo &&
                    x.Estado == EstadoDatasetVersion.Activa &&
                    x.Id != version.Id,
                    ct);
            if (activaActual is not null)
            {
                activaActual.Archivar(usuarioId);
                // Libera el índice único parcial dentro de la misma transacción.
                await db.SaveChangesAsync(ct);
            }

            if (version.Estado == EstadoDatasetVersion.Archivada)
                version.ReactivarDesdeArchivada(usuarioId);
            else
                version.Activar(usuarioId);

            ResumenReconciliacionDto resumen;
            if (tienePredios)
            {
                resumen = await ReconciliarAsync(version, usuarioId, ct);
            }
            else
            {
                const string motivo = "Esquema municipal sin capa de predios.";
                resumen = new ResumenReconciliacionDto(0, 0, 0, 0, true, motivo);
                LogReconciliacionOmitida(logger, version.Id, version.MunicipioCodigo, motivo);
            }
            version.RegistrarResumenReconciliacion(JsonSerializer.Serialize(resumen, JsonOptions));

            await db.SaveChangesAsync(ct);
            await transaccion.CommitAsync(ct);
            return Result.Success(new ActivarVersionImportacionDto(version.Id, version.Estado.ToString(), resumen));
        }
        catch (OperationCanceledException)
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            db.ChangeTracker.Clear();
            return Result.Failure<ActivarVersionImportacionDto>(
                VersionImportacionErrores.ReconciliacionInvalida(ex.Message));
        }
    }

    private async Task<ResumenReconciliacionDto> ReconciliarAsync(
        DatasetVersion version,
        Guid usuarioId,
        CancellationToken ct)
    {
        var capas = await db.CapasParcelas
            .AsNoTracking()
            .Where(x => x.DatasetVersionId == version.Id)
            .OrderBy(x => x.FilaOrigen)
            .ToListAsync(ct);
        var maestro = await db.Predios
            .Where(x => x.MunicipioCodigo == version.MunicipioCodigo)
            .ToListAsync(ct);
        var maestroPorTriplete = maestro.ToDictionary(x => (x.CodUv, x.CodMan, x.CodPred));
        var razonesInvalidas = await ObtenerRazonesInvalidasAsync(version.Id, ct);
        var tripletesVersion = new HashSet<(int CodUv, int CodMan, int CodPred)>();

        var altas = 0;
        var actualizadas = 0;
        var sinCambio = 0;

        foreach (var capa in capas)
        {
            var triplete = (capa.CodUv, capa.CodMan, capa.CodPred);
            if (!tripletesVersion.Add(triplete))
                throw new InvalidOperationException($"Triplete duplicado durante reconciliación: {triplete}.");

            var geometria = GeometriaPredial.CrearDesdeImportacion(capa.Geometria);
            if (geometria.IsFailure)
                throw new DomainException(geometria.Error.Message);

            var superficie = capa.Superficie
                ?? throw new InvalidOperationException($"Fila {capa.FilaOrigen} sin superficie declarada.");
            if (superficie <= 0)
                throw new InvalidOperationException($"Fila {capa.FilaOrigen} con superficie no positiva.");

            razonesInvalidas.TryGetValue(capa.FilaOrigen, out var razonInvalida);
            var detalleInvalida = razonInvalida is null
                ? null
                : $"Geometría inválida en importación versión {version.NumeroVersion}: {razonInvalida}";
            maestroPorTriplete.TryGetValue(triplete, out var predio);
            var ubicacion = CrearUbicacion(capa, predio?.Ubicacion.Referencia);
            var superficieSig = Convert.ToDecimal(capa.Geometria.Area, CultureInfo.InvariantCulture);

            if (predio is null)
            {
                var alta = Predio.CrearDesdeDataset(
                    version.MunicipioCodigo,
                    ubicacion,
                    superficie,
                    superficieSig,
                    geometria.Value,
                    version.Id,
                    usuarioId,
                    capa.NombrePropietarioOrigen,
                    capa.TipoInmueble,
                    capa.CodigoGeografico,
                    detalleInvalida);
                if (alta.IsFailure)
                    throw new DomainException(alta.Error.Message);

                db.Predios.Add(alta.Value);
                altas++;
                continue;
            }

            var actualizacion = predio.ReconciliarDesdeDataset(
                ubicacion,
                superficie,
                superficieSig,
                geometria.Value,
                version.Id,
                capa.NombrePropietarioOrigen,
                capa.TipoInmueble,
                capa.CodigoGeografico,
                detalleInvalida);
            if (actualizacion.IsFailure)
                throw new DomainException(actualizacion.Error.Message);

            if (actualizacion.Value)
                actualizadas++;
            else
                sinCambio++;
        }

        var ausencias = 0;
        foreach (var predio in maestro.Where(x => !tripletesVersion.Contains((x.CodUv, x.CodMan, x.CodPred))))
        {
            predio.MarcarAusenteEnDataset(version.NumeroVersion);
            ausencias++;
        }

        return new ResumenReconciliacionDto(altas, actualizadas, sinCambio, ausencias);
    }

    private async Task<IReadOnlyDictionary<int, string>> ObtenerRazonesInvalidasAsync(
        Guid datasetVersionId,
        CancellationToken ct)
    {
        var filas = await db.Database.SqlQuery<RazonGeometriaSql>($"""
            SELECT fila_origen AS fila_origen,
                   ST_IsValidReason(geometria) AS razon
            FROM dominio.capa_parcelas
            WHERE dataset_version_id = {datasetVersionId}
              AND NOT ST_IsValid(geometria)
            """).ToListAsync(ct);
        return filas.ToDictionary(x => x.FilaOrigen, x => x.Razon);
    }

    private static UbicacionCatastral CrearUbicacion(CapaParcela capa, string? referencia)
    {
        var resultado = UbicacionCatastral.Crear(
            capa.CodUv.ToString(CultureInfo.InvariantCulture),
            capa.CodMan.ToString(CultureInfo.InvariantCulture),
            capa.CodPred.ToString(CultureInfo.InvariantCulture),
            capa.DireccionBarrio,
            capa.DireccionUrbana ?? capa.NombreVia,
            referencia);
        if (resultado.IsFailure)
            throw new DomainException(resultado.Error.Message);
        return resultado.Value;
    }

    private static ReportePreliminarVersionDto? DeserializarReporte(string reporte)
    {
        try
        {
            return JsonSerializer.Deserialize<ReportePreliminarVersionDto>(reporte, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class RazonGeometriaSql
    {
        public int FilaOrigen { get; init; }
        public string Razon { get; init; } = string.Empty;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Reconciliación de predios omitida para versión {DatasetVersionId}, municipio {MunicipioCodigo}: {Motivo}")]
    private static partial void LogReconciliacionOmitida(
        ILogger logger,
        Guid datasetVersionId,
        string municipioCodigo,
        string motivo);
}
