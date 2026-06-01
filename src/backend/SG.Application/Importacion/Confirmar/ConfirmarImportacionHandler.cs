using System.Globalization;
using MediatR;
using NetTopologySuite.Geometries;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Application.Abstractions.Importacion;
using SG.Application.Importacion.GenerarPreview;
using SG.Contracts.Importacion;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;
using SG.Domain.Catastro.ValueObjects;
using SG.Domain.Common;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Application.Importacion.Confirmar;

/// <summary>
/// Fase 2 de la importación. Re-ejecuta mapeo y clasificación desde el .zip en MinIO
/// y escribe en el dominio. Atómico: un único SaveChangesAsync al final — si algo falla,
/// el catastro no queda a medias (ADR 0036).
/// </summary>
public sealed class ConfirmarImportacionHandler(
    IImportacionRepositorio importaciones,
    IPerfilImportacionRepositorio perfiles,
    IPredioRepositorio predios,
    IShapefileReader shapefileReader,
    IZipExtractor zipExtractor,
    IMapeadorImportacion mapeador,
    IMinioService minio,
    ICurrentUserService currentUser)
    : IRequestHandler<ConfirmarImportacionCommand, Result<ConfirmacionImportacionDto>>
{
    public async Task<Result<ConfirmacionImportacionDto>> Handle(
        ConfirmarImportacionCommand request,
        CancellationToken cancellationToken)
    {
        // ── 1. Cargar importación (tracked) ───────────────────────────────
        var importacion = await importaciones.ObtenerPorIdAsync(request.ImportacionId, cancellationToken);
        if (importacion is null)
            return Result.Failure<ConfirmacionImportacionDto>(ImportacionDomain.ImportacionErrores.NoEncontrada);

        // ── 2. Idempotencia ───────────────────────────────────────────────
        if (importacion.Estado == ImportacionDomain.EstadoImportacion.Confirmada)
            return Result.Failure<ConfirmacionImportacionDto>(ImportacionDomain.ImportacionErrores.YaConfirmada);

        if (importacion.Estado != ImportacionDomain.EstadoImportacion.PreviewGenerado)
            return Result.Failure<ConfirmacionImportacionDto>(ImportacionDomain.ImportacionErrores.EstadoInvalidoParaConfirmar);

        // ── 3. Perfil ─────────────────────────────────────────────────────
        var perfil = await perfiles.ObtenerPorIdAsync(importacion.PerfilId, cancellationToken);
        if (perfil is null)
            return Result.Failure<ConfirmacionImportacionDto>(ImportacionDomain.PerfilImportacionErrores.NoEncontrado);

        // ── 4. Descargar ZIP desde MinIO ──────────────────────────────────
        await using var zipStream = await minio.DescargarAsync(importacion.RutaMinioZip, cancellationToken);
        using var zipBuffer = new MemoryStream();
        await zipStream.CopyToAsync(zipBuffer, cancellationToken);

        var dirTemp = Path.Combine(Path.GetTempPath(), $"sg_confirm_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dirTemp);

        try
        {
            // ── 5. Extraer y re-mapear (proceso completo, no la muestra del preview) ──
            zipBuffer.Position = 0;
            var rutas = zipExtractor.Extraer(zipBuffer, dirTemp, perfil.NombreArchivoShp);
            var registros = shapefileReader.Leer(rutas.RutaShp).ToList();
            var resultados = registros
                .Select((r, i) => mapeador.Mapear(r, perfil, numeroFila: i + 1))
                .ToList();

            var usuarioId = currentUser.UserId ?? Guid.Empty;
            Conteos conteos;

            if (perfil.TipoCapa == ImportacionDomain.TipoCapa.Predios)
                conteos = await ConfirmarCapaPrediosAsync(resultados, usuarioId, cancellationToken);
            else
                conteos = await ConfirmarCapaConstruccionesAsync(resultados, usuarioId, cancellationToken);

            // ── 6. Actualizar Importacion — dentro de la misma transacción ──
            importacion.RegistrarConteosConfirmacion(
                filasCreadas:        conteos.Creadas,
                filasActualizadas:   conteos.Actualizadas,
                filasOmitidas:       conteos.Omitidas,
                filasRechazadas:     conteos.Rechazadas,
                filasConAdvertencia: conteos.ConAdvertencia);
            importacion.Confirmar();

            // ── 7. Un único SaveChangesAsync — todo o nada (ADR 0036) ─────
            await predios.GuardarCambiosAsync(cancellationToken);

            return Result.Success(new ConfirmacionImportacionDto(
                importacion.Id,
                TotalFilas: resultados.Count,
                FilasCreadas: conteos.Creadas,
                FilasActualizadas: conteos.Actualizadas,
                FilasOmitidas: conteos.Omitidas,
                FilasRechazadas: conteos.Rechazadas,
                FilasConAdvertencia: conteos.ConAdvertencia));
        }
        finally
        {
            if (Directory.Exists(dirTemp))
                Directory.Delete(dirTemp, recursive: true);
        }
    }

    // ── Capa de predios ────────────────────────────────────────────────────

    private async Task<Conteos> ConfirmarCapaPrediosAsync(
        List<ResultadoMapeoFila> resultados,
        Guid usuarioId,
        CancellationToken ct)
    {
        // Cargar predios existentes CON tracking para el ActualizarDesdeImportacion.
        var tripletasConsulta = resultados
            .Where(r => r.Clasificacion != ClasificacionFila.Rechazada)
            .Select(r => ClasificadorAccionPreview.ExtraerTripleta(r.ValoresMapeados))
            .OfType<(string, string, string)>()
            .Distinct()
            .ToList();

        var existentesCompletos = tripletasConsulta.Count > 0
            ? await predios.ObtenerParaActualizarPorTripletasAsync(tripletasConsulta, ct)
            : new Dictionary<(string, string, string), Predio>();

        // Diccionario de estados para el clasificador.
        var estadosExistentes = existentesCompletos
            .ToDictionary(k => k.Key, v => v.Value.Estado);

        int creadas = 0, actualizadas = 0, omitidas = 0, rechazadas = 0, conAdvertencia = 0;

        foreach (var resultado in resultados)
        {
            bool tieneAdv = resultado.Advertencias.Count > 0;
            var accion = ClasificadorAccionPreview.Clasificar(resultado, estadosExistentes);

            switch (accion)
            {
                case AccionPreviewFila.Crear:
                    if (CrearPredioDesdeResultado(resultado, usuarioId) is { } nuevoPredio)
                    {
                        predios.Agregar(nuevoPredio);
                        creadas++;
                        if (tieneAdv) conAdvertencia++;
                    }
                    else
                    {
                        rechazadas++;
                    }
                    break;

                case AccionPreviewFila.Actualizar:
                    var tripletaAct = ClasificadorAccionPreview.ExtraerTripleta(resultado.ValoresMapeados)!.Value;
                    if (existentesCompletos.TryGetValue(tripletaAct, out var predioExistente))
                    {
                        ActualizarPredioDesdeResultado(predioExistente, resultado, usuarioId);
                        actualizadas++;
                        if (tieneAdv) conAdvertencia++;
                    }
                    else
                    {
                        rechazadas++;
                    }
                    break;

                case AccionPreviewFila.Omitir:
                    omitidas++;
                    break;

                case AccionPreviewFila.Rechazada:
                    rechazadas++;
                    break;
            }
        }

        return new Conteos(creadas, actualizadas, omitidas, rechazadas, conAdvertencia);
    }

    // ── Capa de construcciones ─────────────────────────────────────────────

    private async Task<Conteos> ConfirmarCapaConstruccionesAsync(
        List<ResultadoMapeoFila> resultados,
        Guid usuarioId,
        CancellationToken ct)
    {
        // Cargar predios padre (tracked) por VinculoPredio.* tripleta.
        var vinculoTripletas = resultados
            .Where(r => r.Clasificacion != ClasificacionFila.Rechazada)
            .Select(r => ExtraerVinculoTripleta(r.ValoresMapeados))
            .OfType<(string, string, string)>()
            .Distinct()
            .ToList();

        var prediosPadre = vinculoTripletas.Count > 0
            ? await predios.ObtenerParaActualizarPorTripletasAsync(vinculoTripletas, ct)
            : new Dictionary<(string, string, string), Predio>();

        int creadas = 0, omitidas = 0, rechazadas = 0, conAdvertencia = 0;

        foreach (var resultado in resultados)
        {
            if (resultado.Clasificacion == ClasificacionFila.Rechazada)
            {
                rechazadas++;
                continue;
            }

            var vinculo = ExtraerVinculoTripleta(resultado.ValoresMapeados);
            if (vinculo is null)
            {
                rechazadas++;
                continue;
            }

            if (!prediosPadre.TryGetValue(vinculo.Value, out var predioPadre))
            {
                // El predio padre no existe en la BD — se registra como advertencia/omitido.
                conAdvertencia++;
                omitidas++;
                continue;
            }

            var constResult = AgregarConstruccionDesdeResultado(predioPadre, resultado);
            if (constResult)
            {
                creadas++;
                if (resultado.Advertencias.Count > 0) conAdvertencia++;
            }
            else
            {
                rechazadas++;
            }
        }

        return new Conteos(creadas, 0, omitidas, rechazadas, conAdvertencia);
    }

    // ── Helpers — creación/actualización de predios ────────────────────────

    private static Predio? CrearPredioDesdeResultado(ResultadoMapeoFila resultado, Guid usuarioId)
    {
        var vals = resultado.ValoresMapeados;

        if (!vals.TryGetValue("UbicacionCatastral.Zona",    out var zona)    || zona    is null) return null;
        if (!vals.TryGetValue("UbicacionCatastral.Manzana", out var manzana) || manzana is null) return null;
        if (!vals.TryGetValue("UbicacionCatastral.Lote",    out var lote)    || lote    is null) return null;

        vals.TryGetValue("UbicacionCatastral.Barrio",    out var barrio);
        vals.TryGetValue("UbicacionCatastral.Direccion", out var direccion);
        vals.TryGetValue("UbicacionCatastral.Referencia", out var referencia);

        var ubResult = UbicacionCatastral.Crear(zona, manzana, lote, barrio, direccion, referencia);
        if (ubResult.IsFailure) return null;

        var superficie = ParseDecimal(vals, "SuperficieDeclarada");
        if (superficie is null or <= 0) return null;

        vals.TryGetValue("PropietarioReferencia",  out var propietario);
        vals.TryGetValue("TipoInmuebleOrigen",     out var tipoInmueble);
        vals.TryGetValue("CodigoOrigen",           out var codigoOrigen);

        bool tieneAdv = resultado.Advertencias.Count > 0;
        string? detalleRevision = tieneAdv
            ? string.Join("; ", resultado.Advertencias)
            : null;

        var predioResult = Predio.CrearImportado(
            ubResult.Value,
            superficie.Value,
            usuarioId,
            propietarioReferencia:  propietario,
            tipoInmuebleOrigen:     tipoInmueble,
            codigoOrigen:           codigoOrigen,
            requiereRevision:       tieneAdv,
            detalleRevision:        detalleRevision);

        if (predioResult.IsFailure) return null;

        var predio = predioResult.Value;

        // Asignar geometría si viene del shapefile y es un polígono válido en SRID 32719.
        var geoResult = IntentarCrearGeometria(resultado.Geometria);
        if (geoResult is not null)
            predio.AsignarGeometria(geoResult, usuarioId);

        return predio;
    }

    private static void ActualizarPredioDesdeResultado(
        Predio predio, ResultadoMapeoFila resultado, Guid usuarioId)
    {
        var vals = resultado.ValoresMapeados;
        var superficie = ParseDecimal(vals, "SuperficieDeclarada");

        vals.TryGetValue("PropietarioReferencia", out var propietario);
        vals.TryGetValue("TipoInmuebleOrigen",    out var tipoInmueble);
        vals.TryGetValue("CodigoOrigen",          out var codigoOrigen);

        bool tieneAdv = resultado.Advertencias.Count > 0;
        string? detalleRevision = tieneAdv
            ? string.Join("; ", resultado.Advertencias)
            : null;

        predio.ActualizarDesdeImportacion(
            superficieDeclarada:   superficie ?? predio.SuperficieDeclarada,
            importadoPor:          usuarioId,
            propietarioReferencia: propietario,
            tipoInmuebleOrigen:    tipoInmueble,
            codigoOrigen:          codigoOrigen,
            requiereRevision:      tieneAdv,
            detalleRevision:       detalleRevision);

        var geoResult = IntentarCrearGeometria(resultado.Geometria);
        if (geoResult is not null)
            predio.AsignarGeometria(geoResult, usuarioId);
    }

    // ── Helpers — construcciones ──────────────────────────────────────────

    private static bool AgregarConstruccionDesdeResultado(Predio predioPadre, ResultadoMapeoFila resultado)
    {
        var vals = resultado.ValoresMapeados;

        var numero  = ParseInt(vals, "Construccion.Numero")  ?? 1;
        var pisos   = ParseInt(vals, "Construccion.Pisos")   ?? 1;
        var area    = ParseDecimal(vals, "Construccion.Area");
        if (area is null or <= 0) return false;

        vals.TryGetValue("Construccion.Bloque",          out var bloque);
        vals.TryGetValue("Construccion.TipoConstruccion", out var tipoConstruccion);

        var result = predioPadre.AgregarConstruccion(numero, pisos, bloque, area.Value, tipoConstruccion);
        return result.IsSuccess;
    }

    // ── Helpers — tripletas ───────────────────────────────────────────────

    private static (string Zona, string Manzana, string Lote)?
        ExtraerVinculoTripleta(IReadOnlyDictionary<string, string?> valores)
    {
        if (!valores.TryGetValue("VinculoPredio.Zona",    out var zona)    || zona    is null) return null;
        if (!valores.TryGetValue("VinculoPredio.Manzana", out var manzana) || manzana is null) return null;
        if (!valores.TryGetValue("VinculoPredio.Lote",    out var lote)    || lote    is null) return null;
        return (zona, manzana, lote);
    }

    // ── Helpers — geometría ───────────────────────────────────────────────

    private static GeometriaPredial? IntentarCrearGeometria(Geometry? geo)
    {
        if (geo is null || geo.IsEmpty) return null;

        Polygon? polygon = geo switch
        {
            Polygon p                                               => p,
            MultiPolygon mp when mp.NumGeometries > 0              =>
                Enumerable.Range(0, mp.NumGeometries)
                    .Select(i => (Polygon)mp.GetGeometryN(i))
                    .MaxBy(p => p.Area),
            _                                                      => null,
        };

        if (polygon is null) return null;

        var result = GeometriaPredial.Crear(polygon);
        return result.IsSuccess ? result.Value : null;
    }

    // ── Helpers — parseo de strings a tipos numéricos ─────────────────────

    private static decimal? ParseDecimal(IReadOnlyDictionary<string, string?> vals, string key)
    {
        if (!vals.TryGetValue(key, out var str) || str is null) return null;
        return decimal.TryParse(str, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
            ? v : null;
    }

    private static int? ParseInt(IReadOnlyDictionary<string, string?> vals, string key)
    {
        if (!vals.TryGetValue(key, out var str) || str is null) return null;
        return int.TryParse(str, out var v) ? v : null;
    }

    // ── Tipo auxiliar ─────────────────────────────────────────────────────

    private readonly record struct Conteos(
        int Creadas,
        int Actualizadas,
        int Omitidas,
        int Rechazadas,
        int ConAdvertencia);
}
