using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NetTopologySuite.Geometries;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Importacion;
using SG.Application.Importacion.Versiones;
using SG.Contracts.Importacion;
using SG.Domain.Common;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Infrastructure.Importacion;

internal sealed class CargaVersionadaServicio(
    ApplicationDbContext db,
    IDatasetVersionRepositorio versiones,
    IPerfilImportacionRepositorio perfiles,
    IMinioService minio,
    IZipExtractor zipExtractor,
    IShapefileReader shapefileReader,
    IReportePreviewVersionServicio reportePreview,
    IEsquemaCapasMunicipioRepositorio esquemas,
    IInspectorPaqueteVersionado inspectorPaquete,
    IConfiguration configuration) : ICargaVersionadaServicio
{
    private const int TamanoLote = 1000;

    public async Task CargarAsync(Guid datasetVersionId, CancellationToken ct = default)
    {
        var version = await versiones.ObtenerPorIdAsync(datasetVersionId, ct)
            ?? throw new InvalidOperationException("DatasetVersion no encontrada para carga.");

        if (version.Estado != EstadoDatasetVersion.EnCarga)
            return;

        if (string.IsNullOrWhiteSpace(version.RutaMinioPaquete))
            throw new InvalidOperationException("DatasetVersion no tiene paquete almacenado en MinIO.");

        var esquemaMunicipal = await esquemas.ListarAsync(version.MunicipioCodigo, ct);
        if (esquemaMunicipal.Count == 0)
            throw new InvalidOperationException($"No existe esquema de capas para el municipio {version.MunicipioCodigo}.");
        var perfilesVersionados = await ObtenerPerfilesVersionadosAsync(esquemaMunicipal, ct);
        await using var paqueteStream = await minio.DescargarAsync(version.RutaMinioPaquete, ct);
        using var paquete = new MemoryStream();
        await paqueteStream.CopyToAsync(paquete, ct);
        var inspeccion = inspectorPaquete.Inspeccionar(paquete, esquemaMunicipal);
        if (!inspeccion.EsValido)
            throw new InvalidOperationException($"El paquete almacenado no coincide con el esquema: {string.Join(" ", inspeccion.Errores)}");

        var directorioTemporal = Path.Combine(Path.GetTempPath(), $"sg_version_{datasetVersionId:N}");
        Directory.CreateDirectory(directorioTemporal);
        var conteos = new Dictionary<string, int>(StringComparer.Ordinal);

        try
        {
            foreach (var definicion in esquemaMunicipal.Where(x =>
                         inspeccion.PerfilesPresentes.Contains(x.NombrePerfil)))
            {
                var perfil = perfilesVersionados[definicion.NombrePerfil];
                version.RegistrarProgreso(SerializarReporte(definicion.TablaDestino, conteos));
                await versiones.GuardarCambiosAsync(ct);

                paquete.Position = 0;
                var rutas = zipExtractor.Extraer(paquete, directorioTemporal, definicion.NombreArchivoShp);
                var registros = shapefileReader.Leer(rutas.RutaShp).ToList();
                await using var contextoCarga = CrearContextoCargaSinInterceptors();
                await InsertarCapaAsync(contextoCarga, version.Id, definicion, perfil, registros, ct);
                version = await versiones.ObtenerPorIdAsync(datasetVersionId, ct)
                    ?? throw new InvalidOperationException("DatasetVersion no encontrada después de insertar una capa.");

                var insertados = await ContarFilasCapaAsync(contextoCarga, version.Id, definicion.TipoCapa, ct);
                if (insertados != registros.Count)
                    throw new InvalidOperationException(
                        $"Conteo inconsistente en {definicion.TablaDestino}: SHP={registros.Count}, insertadas={insertados}.");

                conteos[definicion.TablaDestino] = insertados;
                version.RegistrarProgreso(SerializarReporte(null, conteos));
                await versiones.GuardarCambiosAsync(ct);
            }

            var reporteCompleto = await reportePreview.GenerarAsync(datasetVersionId, ct);
            version.RegistrarReportePreview(JsonSerializer.Serialize(reporteCompleto));
            version.MarcarPreviewListo();
            await versiones.GuardarCambiosAsync(ct);
        }
        finally
        {
            if (Directory.Exists(directorioTemporal))
                Directory.Delete(directorioTemporal, recursive: true);
        }
    }

    public async Task MarcarHuerfanasAlArrancarAsync(CancellationToken ct = default)
    {
        var huerfanas = await versiones.ObtenerEnCargaAsync(ct);
        foreach (var version in huerfanas)
            await MarcarFallidaYPurgarAsync(version.Id, "carga interrumpida por reinicio", ct);
    }

    public async Task MarcarFallidaYPurgarAsync(Guid datasetVersionId, string errorCarga, CancellationToken ct = default)
    {
        var version = await versiones.ObtenerPorIdAsync(datasetVersionId, ct);
        if (version is null || version.Estado != EstadoDatasetVersion.EnCarga)
            return;

        var errorSeguro = string.IsNullOrWhiteSpace(errorCarga)
            ? "error no especificado durante la carga"
            : errorCarga[..Math.Min(errorCarga.Length, 2000)];
        version.RegistrarErrorCarga(errorSeguro);
        version.MarcarFallida();
        await versiones.GuardarCambiosAsync(ct);

        // El trigger permite DELETE únicamente para EnCarga/Fallida/Descartada.
        await PurgarCapasAsync(datasetVersionId, ct);
    }

    public async Task<Result<DescartarVersionImportacionDto>> DescartarYPurgarAsync(
        Guid datasetVersionId,
        CancellationToken ct = default)
    {
        await using var transaccion = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var version = await versiones.ObtenerPorIdAsync(datasetVersionId, ct);
            if (version is null)
            {
                await transaccion.RollbackAsync(ct);
                return Result.Failure<DescartarVersionImportacionDto>(
                    VersionImportacionErrores.NoEncontrada);
            }

            if (version.Estado != EstadoDatasetVersion.PreviewListo)
            {
                await transaccion.RollbackAsync(ct);
                return Result.Failure<DescartarVersionImportacionDto>(
                    VersionImportacionErrores.EstadoNoDescartable);
            }

            version.Descartar();
            await versiones.GuardarCambiosAsync(ct);
            await PurgarCapasAsync(datasetVersionId, ct);
            await transaccion.CommitAsync(ct);

            return Result.Success(new DescartarVersionImportacionDto(
                version.Id,
                version.Estado.ToString()));
        }
        catch (OperationCanceledException)
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            throw;
        }
        catch
        {
            await transaccion.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task PurgarCapasAsync(Guid datasetVersionId, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dominio.capa_parcelas WHERE dataset_version_id = {datasetVersionId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dominio.capa_edificaciones WHERE dataset_version_id = {datasetVersionId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dominio.capa_predios_no_fotografiados WHERE dataset_version_id = {datasetVersionId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dominio.capa_manzanas WHERE dataset_version_id = {datasetVersionId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dominio.capa_distritos WHERE dataset_version_id = {datasetVersionId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dominio.capa_zonas WHERE dataset_version_id = {datasetVersionId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dominio.capa_vias WHERE dataset_version_id = {datasetVersionId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dominio.capa_areas_urbanas WHERE dataset_version_id = {datasetVersionId}", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM dominio.capa_puntos_geodesicos WHERE dataset_version_id = {datasetVersionId}", ct);
    }

    private async Task<Dictionary<string, PerfilImportacion>> ObtenerPerfilesVersionadosAsync(
        IReadOnlyList<EsquemaCapaMunicipio> esquemaMunicipal,
        CancellationToken ct)
    {
        var disponibles = await perfiles.ListarAsync(ct);
        var resultado = new Dictionary<string, PerfilImportacion>(StringComparer.Ordinal);
        foreach (var definicion in esquemaMunicipal)
        {
            var perfil = disponibles.FirstOrDefault(x => x.Nombre == definicion.NombrePerfil)
                ?? throw new InvalidOperationException($"No existe el perfil '{definicion.NombrePerfil}'.");
            if (perfil.TipoCapa != definicion.TipoCapa)
                throw new InvalidOperationException(
                    $"El perfil '{definicion.NombrePerfil}' no corresponde a {definicion.TipoCapa}.");
            resultado.Add(definicion.NombrePerfil, perfil);
        }
        return resultado;
    }

    private static async Task InsertarCapaAsync(
        ApplicationDbContext contextoCarga,
        Guid datasetVersionId,
        EsquemaCapaMunicipio definicion,
        PerfilImportacion perfil,
        IReadOnlyList<RegistroCrudoShapefile> registros,
        CancellationToken ct)
    {
        switch (definicion.TipoCapa)
        {
            case TipoCapa.Predios:
                await InsertarEnLotesAsync(contextoCarga, contextoCarga.CapasParcelas, registros.Select((r, i) => CrearParcela(datasetVersionId, perfil, r, i + 1)), ct);
                break;
            case TipoCapa.Construcciones:
                await InsertarEnLotesAsync(contextoCarga, contextoCarga.CapasEdificaciones, registros.Select((r, i) => CrearEdificacion(datasetVersionId, perfil, r, i + 1)), ct);
                break;
            case TipoCapa.PrediosNoFotografiados:
                await InsertarEnLotesAsync(contextoCarga, contextoCarga.CapasPrediosNoFotografiados, registros.Select((r, i) => CrearPredioNoFotografiado(datasetVersionId, perfil, r, i + 1)), ct);
                break;
            case TipoCapa.Manzanas:
                await InsertarEnLotesAsync(contextoCarga, contextoCarga.CapasManzanas, registros.Select((r, i) => CrearManzana(datasetVersionId, perfil, r, i + 1)), ct);
                break;
            case TipoCapa.Distritos:
                await InsertarEnLotesAsync(contextoCarga, contextoCarga.CapasDistritos, registros.Select((r, i) => CrearDistrito(datasetVersionId, perfil, r, i + 1)), ct);
                break;
            case TipoCapa.ZonasValuacion:
                await InsertarEnLotesAsync(contextoCarga, contextoCarga.CapasZonas, registros.Select((r, i) => CrearZona(datasetVersionId, perfil, r, i + 1)), ct);
                break;
            case TipoCapa.Vias:
                await InsertarEnLotesAsync(contextoCarga, contextoCarga.CapasVias, registros.Select((r, i) => CrearVia(datasetVersionId, perfil, r, i + 1)), ct);
                break;
            case TipoCapa.AreasUrbanas:
                await InsertarEnLotesAsync(contextoCarga, contextoCarga.CapasAreasUrbanas, registros.Select((r, i) => CrearAreaUrbana(datasetVersionId, perfil, r, i + 1)), ct);
                break;
            case TipoCapa.PuntosGeodesicos:
                await InsertarEnLotesAsync(contextoCarga, contextoCarga.CapasPuntosGeodesicos, registros.Select((r, i) => CrearPuntoGeodesico(datasetVersionId, perfil, r, i + 1)), ct);
                break;
            default:
                throw new InvalidOperationException($"Tipo de capa no soportado: {definicion.TipoCapa}.");
        }

    }

    private static async Task InsertarEnLotesAsync<T>(ApplicationDbContext contextoCarga, DbSet<T> conjunto, IEnumerable<T> filas, CancellationToken ct)
        where T : class
    {
        foreach (var lote in filas.Chunk(TamanoLote))
        {
            conjunto.AddRange(lote);
            await contextoCarga.SaveChangesAsync(ct);
            contextoCarga.ChangeTracker.Clear();
        }
    }

    private static Task<int> ContarFilasCapaAsync(ApplicationDbContext contextoCarga, Guid datasetVersionId, TipoCapa tipoCapa, CancellationToken ct) =>
        tipoCapa switch
        {
            TipoCapa.Predios => contextoCarga.CapasParcelas.CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
            TipoCapa.Construcciones => contextoCarga.CapasEdificaciones.CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
            TipoCapa.PrediosNoFotografiados => contextoCarga.CapasPrediosNoFotografiados.CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
            TipoCapa.Manzanas => contextoCarga.CapasManzanas.CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
            TipoCapa.Distritos => contextoCarga.CapasDistritos.CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
            TipoCapa.ZonasValuacion => contextoCarga.CapasZonas.CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
            TipoCapa.Vias => contextoCarga.CapasVias.CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
            TipoCapa.AreasUrbanas => contextoCarga.CapasAreasUrbanas.CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
            TipoCapa.PuntosGeodesicos => contextoCarga.CapasPuntosGeodesicos.CountAsync(x => x.DatasetVersionId == datasetVersionId, ct),
            _ => throw new InvalidOperationException($"Tipo de capa no soportado: {tipoCapa}."),
        };

    private ApplicationDbContext CrearContextoCargaSinInterceptors()
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("La cadena de conexión no está disponible para la carga.");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new ApplicationDbContext(options);
    }

    private static CapaParcela CrearParcela(Guid versionId, PerfilImportacion perfil, RegistroCrudoShapefile r, int fila) =>
        CapaParcela.Crear(versionId, PoligonoParcela(r, "capa_parcelas", fila), AEntero(r, perfil, "CapaParcela.CodUv", fila),
            AEntero(r, perfil, "CapaParcela.CodMan", fila), AEntero(r, perfil, "CapaParcela.CodPred", fila),
            Extra(r, perfil), fila, Texto(r, perfil, "CapaParcela.CodigoGeografico"),
            ADecimal(r, perfil, "CapaParcela.Superficie"), AEnteroOpcional(r, perfil, "CapaParcela.ValuacionZonal"),
            Texto(r, perfil, "CapaParcela.TipoInmueble"), Texto(r, perfil, "CapaParcela.ServicioAlcantarillado"),
            Texto(r, perfil, "CapaParcela.ServicioAgua"), Texto(r, perfil, "CapaParcela.ServicioLuz"),
            Texto(r, perfil, "CapaParcela.ServicioTelefonia"), Texto(r, perfil, "CapaParcela.NombrePropietarioOrigen"),
            Texto(r, perfil, "CapaParcela.NombreVia"), Texto(r, perfil, "CapaParcela.DireccionBarrio"),
            Texto(r, perfil, "CapaParcela.DireccionUrbana"), Texto(r, perfil, "CapaParcela.UsoTerreno"),
            Texto(r, perfil, "CapaParcela.TopografiaTerreno"));

    private static CapaEdificacion CrearEdificacion(Guid versionId, PerfilImportacion perfil, RegistroCrudoShapefile r, int fila) =>
        CapaEdificacion.Crear(versionId, MultiPoligonoAuxiliar(r, "capa_edificaciones", fila), Extra(r, perfil), fila,
            ALongOpcional(r, perfil, "CapaEdificacion.IdEdificacionOrigen"), Texto(r, perfil, "CapaEdificacion.CodigoGeografico"),
            AEnteroOpcional(r, perfil, "CapaEdificacion.CodUv"), AEnteroOpcional(r, perfil, "CapaEdificacion.CodMan"),
            AEnteroOpcional(r, perfil, "CapaEdificacion.CodPred"), ALongOpcional(r, perfil, "CapaEdificacion.NumeroEdificacion"),
            ALongOpcional(r, perfil, "CapaEdificacion.Piso"), Texto(r, perfil, "CapaEdificacion.CodigoEspacio"),
            ALongOpcional(r, perfil, "CapaEdificacion.CodigoBloque"), ADecimal(r, perfil, "CapaEdificacion.AreaConstruida"));

    private static CapaPredioNoFotografiado CrearPredioNoFotografiado(Guid versionId, PerfilImportacion perfil, RegistroCrudoShapefile r, int fila) =>
        CapaPredioNoFotografiado.Crear(versionId, MultiPoligonoAuxiliar(r, "capa_predios_no_fotografiados", fila), Extra(r, perfil), fila,
            ALongOpcional(r, perfil, "CapaPredioNoFotografiado.IdPredioOrigen"), Texto(r, perfil, "CapaPredioNoFotografiado.CodigoGeografico"),
            AEnteroOpcional(r, perfil, "CapaPredioNoFotografiado.CodUv"), AEnteroOpcional(r, perfil, "CapaPredioNoFotografiado.CodMan"),
            AEnteroOpcional(r, perfil, "CapaPredioNoFotografiado.CodPred"), Texto(r, perfil, "CapaPredioNoFotografiado.IndicadorFotos"),
            Texto(r, perfil, "CapaPredioNoFotografiado.FotoFrente"), Texto(r, perfil, "CapaPredioNoFotografiado.FotoDerecha"),
            Texto(r, perfil, "CapaPredioNoFotografiado.FotoIzquierda"));

    private static CapaManzana CrearManzana(Guid versionId, PerfilImportacion perfil, RegistroCrudoShapefile r, int fila) =>
        CapaManzana.Crear(versionId, MultiPoligonoAuxiliar(r, "capa_manzanas", fila), Extra(r, perfil), fila,
            Texto(r, perfil, "CapaManzana.CodigoGeografico"), AEnteroOpcional(r, perfil, "CapaManzana.CodUv"),
            AEnteroOpcional(r, perfil, "CapaManzana.CodMan"), ADecimal(r, perfil, "CapaManzana.CoordenadaOrigen"));

    private static CapaDistrito CrearDistrito(Guid versionId, PerfilImportacion perfil, RegistroCrudoShapefile r, int fila) =>
        CapaDistrito.Crear(versionId, MultiPoligonoAuxiliar(r, "capa_distritos", fila), Extra(r, perfil), fila,
            Texto(r, perfil, "CapaDistrito.CodigoGeografico"), AEnteroOpcional(r, perfil, "CapaDistrito.CodUv"),
            Texto(r, perfil, "CapaDistrito.Nombre"));

    private static CapaZona CrearZona(Guid versionId, PerfilImportacion perfil, RegistroCrudoShapefile r, int fila) =>
        CapaZona.Crear(versionId, MultiPoligonoAuxiliar(r, "capa_zonas", fila), Extra(r, perfil), fila,
            Texto(r, perfil, "CapaZona.NombreZona"), ALongOpcional(r, perfil, "CapaZona.IdZonaOrigen"),
            Texto(r, perfil, "CapaZona.CodigoGeografico"));

    private static CapaVia CrearVia(Guid versionId, PerfilImportacion perfil, RegistroCrudoShapefile r, int fila) =>
        CapaVia.Crear(versionId, MultiLineaAuxiliar(r, "capa_vias", fila), Extra(r, perfil), fila,
            Texto(r, perfil, "CapaVia.Material"), Texto(r, perfil, "CapaVia.Nombre"),
            Texto(r, perfil, "CapaVia.Tipo"), ADecimal(r, perfil, "CapaVia.DistanciaOrigen"));

    private static CapaAreaUrbana CrearAreaUrbana(
        Guid versionId,
        PerfilImportacion perfil,
        RegistroCrudoShapefile r,
        int fila) =>
        CapaAreaUrbana.Crear(
            versionId,
            GeometriaPoligonalAuxiliar(r, "capa_areas_urbanas", fila),
            Extra(r, perfil),
            fila);

    private static CapaPuntoGeodesico CrearPuntoGeodesico(
        Guid versionId,
        PerfilImportacion perfil,
        RegistroCrudoShapefile r,
        int fila) =>
        CapaPuntoGeodesico.Crear(
            versionId,
            PuntoAuxiliar(r, "capa_puntos_geodesicos", fila),
            Extra(r, perfil),
            fila);

    private static Polygon PoligonoParcela(RegistroCrudoShapefile registro, string capa, int fila) =>
        registro.Geometria switch
        {
            Polygon poligono => poligono,
            MultiPolygon { NumGeometries: 1 } multipoligono => (Polygon)multipoligono.GetGeometryN(0),
            null => throw new InvalidOperationException(
                $"Capa '{capa}', fila {fila}: geometría nula; se esperaba Polygon (se admite MultiPolygon de una sola parte)."),
            Geometry geometria => throw new InvalidOperationException(
                $"Capa '{capa}', fila {fila}: llegó {DescribirTipo(geometria)}; se esperaba Polygon (se admite MultiPolygon de una sola parte)."),
        };

    private static MultiPolygon? MultiPoligonoAuxiliar(
        RegistroCrudoShapefile registro,
        string capa,
        int fila) => registro.Geometria switch
        {
            null => null,
            Polygon poligono => new MultiPolygon([poligono]) { SRID = poligono.SRID },
            MultiPolygon multipoligono => multipoligono,
            Geometry geometria => throw new InvalidOperationException(
                $"Capa '{capa}', fila {fila}: llegó {DescribirTipo(geometria)}; se esperaba Polygon o MultiPolygon."),
        };

    private static MultiLineString? MultiLineaAuxiliar(
        RegistroCrudoShapefile registro,
        string capa,
        int fila) => registro.Geometria switch
        {
            null => null,
            LineString linea => new MultiLineString([linea]) { SRID = linea.SRID },
            MultiLineString multiLinea => multiLinea,
            Geometry geometria => throw new InvalidOperationException(
                $"Capa '{capa}', fila {fila}: llegó {DescribirTipo(geometria)}; se esperaba LineString o MultiLineString."),
        };

    private static Geometry? GeometriaPoligonalAuxiliar(
        RegistroCrudoShapefile registro,
        string capa,
        int fila) => registro.Geometria switch
        {
            null => null,
            Polygon poligono => poligono,
            MultiPolygon multipoligono => multipoligono,
            Geometry geometria => throw new InvalidOperationException(
                $"Capa '{capa}', fila {fila}: llegó {DescribirTipo(geometria)}; se esperaba Polygon o MultiPolygon."),
        };

    private static Point? PuntoAuxiliar(
        RegistroCrudoShapefile registro,
        string capa,
        int fila) => registro.Geometria switch
        {
            null => null,
            Point punto => punto,
            Geometry geometria => throw new InvalidOperationException(
                $"Capa '{capa}', fila {fila}: llegó {DescribirTipo(geometria)}; se esperaba Point."),
        };

    private static string DescribirTipo(Geometry geometria) => geometria switch
    {
        MultiPolygon multipoligono => $"MultiPolygon de {multipoligono.NumGeometries} partes",
        MultiLineString multiLinea => $"MultiLineString de {multiLinea.NumGeometries} partes",
        _ => geometria.GeometryType,
    };

    private static string Extra(RegistroCrudoShapefile registro, PerfilImportacion perfil)
    {
        var mapeadas = perfil.Mapeos.Select(x => x.NombreColumnaOrigen).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var extras = registro.Atributos.Where(x => !mapeadas.Contains(x.Key))
            .ToDictionary(x => x.Key, x => ValorJsonSeguro(x.Value), StringComparer.OrdinalIgnoreCase);
        return JsonSerializer.Serialize(extras);
    }

    private static object? ValorJsonSeguro(object? valor) => valor switch
    {
        DBNull => null,
        double numero when double.IsNaN(numero) || double.IsInfinity(numero) => null,
        float numero when float.IsNaN(numero) || float.IsInfinity(numero) => null,
        _ => valor,
    };

    private static string? Texto(RegistroCrudoShapefile registro, PerfilImportacion perfil, string destino) =>
        Valor(registro, perfil, destino)?.ToString()?.Trim();

    private static int AEntero(RegistroCrudoShapefile registro, PerfilImportacion perfil, string destino, int fila) =>
        AEnteroOpcional(registro, perfil, destino)
            ?? throw new InvalidOperationException($"La fila {fila} no tiene entero válido para {destino}.");

    private static int? AEnteroOpcional(RegistroCrudoShapefile registro, PerfilImportacion perfil, string destino) =>
        Convertir<int>(Valor(registro, perfil, destino), int.TryParse);

    private static long? ALongOpcional(RegistroCrudoShapefile registro, PerfilImportacion perfil, string destino) =>
        Convertir<long>(Valor(registro, perfil, destino), long.TryParse);

    private static decimal? ADecimal(RegistroCrudoShapefile registro, PerfilImportacion perfil, string destino) =>
        Convertir<decimal>(Valor(registro, perfil, destino), (string value, out decimal result) =>
            decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out result));

    private delegate bool TryParseDelegate<T>(string value, out T result);

    private static T? Convertir<T>(object? valor, TryParseDelegate<T> parser) where T : struct
    {
        if (valor is null || valor is DBNull)
            return null;
        var texto = Convert.ToString(valor, CultureInfo.InvariantCulture);
        return texto is not null && parser(texto, out var resultado) ? resultado : null;
    }

    private static object? Valor(RegistroCrudoShapefile registro, PerfilImportacion perfil, string destino)
    {
        var mapeo = perfil.Mapeos.FirstOrDefault(x => x.CampoDestino == destino);
        return mapeo is not null && registro.Atributos.TryGetValue(mapeo.NombreColumnaOrigen, out var valor)
            ? valor
            : null;
    }

    private static string SerializarReporte(string? capaEnCurso, IReadOnlyDictionary<string, int> conteos) =>
        JsonSerializer.Serialize(new ReportePreliminarVersionDto(capaEnCurso, conteos));
}
