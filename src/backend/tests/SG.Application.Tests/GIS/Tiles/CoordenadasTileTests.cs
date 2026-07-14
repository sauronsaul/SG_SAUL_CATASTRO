using FluentAssertions;
using SG.Application.GIS.Tiles;

namespace SG.Application.Tests.GIS.Tiles;

public sealed class CoordenadasTileTests
{
    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(22, 0, 0)]
    [InlineData(22, 4194303, 4194303)]
    public void SonValidas_CoordenadasDentroDelMundo_RetornaTrue(int z, int x, int y)
    {
        CoordenadasTile.SonValidas(z, x, y).Should().BeTrue();
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(23, 0, 0)]
    [InlineData(1, -1, 0)]
    [InlineData(1, 0, -1)]
    [InlineData(1, 2, 0)]
    [InlineData(1, 0, 2)]
    public void SonValidas_CoordenadasFueraDeRango_RetornaFalse(int z, int x, int y)
    {
        CoordenadasTile.SonValidas(z, x, y).Should().BeFalse();
    }
}
