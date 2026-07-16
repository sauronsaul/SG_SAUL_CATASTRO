using System.IO.Compression;
using SG.Application.Abstractions.Importacion;
using SG.Domain.Importacion;

namespace SG.Infrastructure.Importacion;

internal sealed class InspectorPaqueteVersionado : IInspectorPaqueteVersionado
{
    private static readonly string[] ExtensionesRequeridas = [".shp", ".dbf", ".shx", ".prj"];

    public ResultadoInspeccionPaqueteVersionado Inspeccionar(
        Stream paquete,
        IReadOnlyList<EsquemaCapaMunicipio> esquemaMunicipal)
    {
        var posicionOriginal = paquete.CanSeek ? paquete.Position : 0;
        try
        {
            if (paquete.CanSeek)
                paquete.Position = 0;

            using var zip = new ZipArchive(paquete, ZipArchiveMode.Read, leaveOpen: true);
            var archivos = zip.Entries
                .Where(x => !string.IsNullOrEmpty(x.Name))
                .Select(x => x.FullName.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var presentes = new HashSet<string>(StringComparer.Ordinal);
            var errores = new List<string>();

            foreach (var definicion in esquemaMunicipal)
            {
                var nombreBase = Path.GetFileNameWithoutExtension(definicion.NombreArchivoShp);
                var esperados = ExtensionesRequeridas.Select(x => nombreBase + x).ToList();
                var encontrados = esperados.Where(archivos.Contains).ToList();

                if (encontrados.Count == esperados.Count)
                {
                    presentes.Add(definicion.NombrePerfil);
                    continue;
                }

                if (!definicion.Obligatoria && encontrados.Count == 0)
                    continue;

                var faltantes = esperados.Except(encontrados, StringComparer.OrdinalIgnoreCase);
                errores.Add($"{definicion.TipoCapa}: faltan {string.Join(", ", faltantes)}.");
            }

            return new ResultadoInspeccionPaqueteVersionado(errores.Count == 0, presentes, errores);
        }
        catch (InvalidDataException ex)
        {
            return new ResultadoInspeccionPaqueteVersionado(
                false,
                new HashSet<string>(StringComparer.Ordinal),
                [$"ZIP inválido: {ex.Message}"]);
        }
        finally
        {
            if (paquete.CanSeek)
                paquete.Position = posicionOriginal;
        }
    }
}
