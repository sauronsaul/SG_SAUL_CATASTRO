namespace SG.Application.Abstractions;

public record RutasShapefile(
    string RutaShp,
    string RutaDbf,
    string RutaShx,
    string? RutaPrj);

public interface IZipExtractor
{
    RutasShapefile Extraer(Stream zipStream, string directorioDestino, string nombreArchivoShp);
}
