using System.Security.Cryptography;
using System.Text;

namespace SG.Application.GIS.Tiles;

public static class ETagTile
{
    public static string Calcular(Guid datasetVersionId, CapaTile capa, int z, int x, int y)
    {
        var clave = string.Create(
            provider: null,
            $"{datasetVersionId:D}|{CatalogoCapasTile.ObtenerNombre(capa)}|{z}|{x}|{y}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(clave));
        return $"\"{Convert.ToHexString(hash).ToLowerInvariant()}\"";
    }
}
