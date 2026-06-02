using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Application.Importacion.GenerarPreview;
using SG.Contracts.Importacion;
using SG.Domain.Catastro.Enums;
using SG.Domain.Importacion;

namespace SG.Application.Importacion;

/// <summary>
/// Pipeline compartido: ZIP (stream) → extraer → leer shapefile → mapear → clasificar.
/// Devuelve la lista completa de filas clasificadas.
/// El caller es responsable de proporcionar un stream seekable posicionado en 0.
/// </summary>
public sealed class PipelineShapefileService(
    IZipExtractor zipExtractor,
    IShapefileReader shapefileReader,
    IMapeadorImportacion mapeador,
    IPredioRepositorio predios)
{
    public async Task<IReadOnlyList<FilaPreviewDto>> ProcesarAsync(
        Stream zipBuffer,
        PerfilImportacion perfil,
        CancellationToken ct)
    {
        var dirTemp = Path.Combine(Path.GetTempPath(), $"sg_pipe_{Guid.NewGuid():N}");
        Directory.CreateDirectory(dirTemp);

        try
        {
            var rutas = zipExtractor.Extraer(zipBuffer, dirTemp, perfil.NombreArchivoShp);

            var resultados = shapefileReader.Leer(rutas.RutaShp)
                .Select((r, i) => mapeador.Mapear(r, perfil, numeroFila: i + 1))
                .ToList();

            // Para construcciones: verificar si el predio padre existe para mostrar
            // Omitir en el preview cuando no existe — consistente con ConfirmarCapaConstruccionesAsync.
            if (perfil.TipoCapa == TipoCapa.Construcciones)
            {
                var vinculoTripletas = resultados
                    .Where(r => r.Clasificacion != ClasificacionFila.Rechazada)
                    .Select(r => ExtraerVinculoTripleta(r.ValoresMapeados))
                    .OfType<(string, string, string)>()
                    .Distinct()
                    .ToList();

                IReadOnlyDictionary<(string, string, string), EstadoPredio> prediosExistentes =
                    vinculoTripletas.Count > 0
                        ? await predios.ObtenerEstadosPorTripletasAsync(vinculoTripletas, ct)
                        : new Dictionary<(string, string, string), EstadoPredio>();

                return resultados
                    .Select(r =>
                    {
                        if (r.Clasificacion == ClasificacionFila.Rechazada)
                            return new FilaPreviewDto(r.NumeroFila, AccionPreviewFila.Rechazada,
                                r.ValoresMapeados, r.Advertencias, r.Errores);

                        var vinculo = ExtraerVinculoTripleta(r.ValoresMapeados);
                        var accion  = vinculo is not null && prediosExistentes.ContainsKey(vinculo.Value)
                            ? AccionPreviewFila.Crear
                            : AccionPreviewFila.Omitir;

                        return new FilaPreviewDto(r.NumeroFila, accion,
                            r.ValoresMapeados, r.Advertencias, r.Errores);
                    })
                    .ToList();
            }

            // Para predios: clasificar según estados en BD (Crear / Actualizar / Omitir).
            var tripletas = resultados
                .Where(r => r.Clasificacion != ClasificacionFila.Rechazada)
                .Select(r => ClasificadorAccionPreview.ExtraerTripleta(r.ValoresMapeados))
                .OfType<(string, string, string)>()
                .Distinct()
                .ToList();

            IReadOnlyDictionary<(string, string, string), EstadoPredio> estados =
                tripletas.Count > 0
                    ? await predios.ObtenerEstadosPorTripletasAsync(tripletas, ct)
                    : new Dictionary<(string, string, string), EstadoPredio>();

            return resultados
                .Select(r => new FilaPreviewDto(
                    r.NumeroFila,
                    ClasificadorAccionPreview.Clasificar(r, estados),
                    r.ValoresMapeados,
                    r.Advertencias,
                    r.Errores))
                .ToList();
        }
        finally
        {
            if (Directory.Exists(dirTemp))
                Directory.Delete(dirTemp, recursive: true);
        }
    }

    private static (string Zona, string Manzana, string Lote)?
        ExtraerVinculoTripleta(IReadOnlyDictionary<string, string?> valores)
    {
        if (!valores.TryGetValue("VinculoPredio.Zona",    out var zona)    || zona    is null) return null;
        if (!valores.TryGetValue("VinculoPredio.Manzana", out var manzana) || manzana is null) return null;
        if (!valores.TryGetValue("VinculoPredio.Lote",    out var lote)    || lote    is null) return null;
        return (zona, manzana, lote);
    }
}
