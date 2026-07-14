namespace SG.Application.GIS.Tiles;

public static class CoordenadasTile
{
    public const int ZoomMinimo = 0;
    public const int ZoomMaximo = 22;

    public static bool SonValidas(int z, int x, int y)
    {
        if (z is < ZoomMinimo or > ZoomMaximo)
            return false;

        var limite = 1 << z;
        return x >= 0 && x < limite && y >= 0 && y < limite;
    }
}
