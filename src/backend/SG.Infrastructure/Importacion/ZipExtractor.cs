using System.IO.Compression;
using SG.Application.Abstractions;

namespace SG.Infrastructure.Importacion;

internal sealed class ZipExtractor : IZipExtractor
{
    public RutasShapefile Extraer(Stream zipStream, string directorioDestino, string nombreArchivoShp)
    {
        Directory.CreateDirectory(directorioDestino);

        using var archivo = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        archivo.ExtractToDirectory(directorioDestino, overwriteFiles: true);

        // Búsqueda por nombre exacto declarado en el perfil — nunca FirstOrDefault silencioso.
        var rutaShp = Path.Combine(directorioDestino, nombreArchivoShp);
        if (!File.Exists(rutaShp))
        {
            var disponibles = Directory
                .EnumerateFiles(directorioDestino, "*.shp", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Order()
                .ToList();
            throw new InvalidOperationException(
                $"El ZIP no contiene '{nombreArchivoShp}' (declarado en el perfil). " +
                $"Archivos .shp encontrados: " +
                $"{(disponibles.Count > 0 ? string.Join(", ", disponibles) : "(ninguno)")}.");
        }

        // Archivos hermanos con el mismo nombre base.
        var baseName = Path.GetFileNameWithoutExtension(nombreArchivoShp);
        var rutaDbf = BuscarHermano(directorioDestino, baseName, ".dbf", requerido: true);
        var rutaShx = BuscarHermano(directorioDestino, baseName, ".shx", requerido: true);
        var rutaPrj = BuscarHermano(directorioDestino, baseName, ".prj", requerido: false);

        return new RutasShapefile(rutaShp, rutaDbf!, rutaShx!, rutaPrj);
    }

    private static string? BuscarHermano(
        string directorio, string baseName, string extension, bool requerido)
    {
        var ruta = Path.Combine(directorio, baseName + extension);
        if (File.Exists(ruta))
            return ruta;
        if (requerido)
            throw new InvalidOperationException(
                $"El shapefile '{baseName}.shp' no tiene el archivo requerido '{baseName}{extension}'.");
        return null;
    }
}
