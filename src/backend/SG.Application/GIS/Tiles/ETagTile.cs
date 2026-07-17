using System.Security.Cryptography;
using System.Text;
using SG.Domain.Importacion;

namespace SG.Application.GIS.Tiles;

public static class ETagTile
{
    public static string Calcular(
        string municipioCodigo,
        Guid datasetVersionId,
        int numeroVersion,
        TipoCapa capa,
        int z,
        int x,
        int y)
    {
        var clave = string.Create(
            provider: null,
            $"{municipioCodigo}|{datasetVersionId:D}|{numeroVersion}|{capa}|{z}|{x}|{y}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(clave));
        return $"\"{Convert.ToHexString(hash).ToLowerInvariant()}\"";
    }
}
