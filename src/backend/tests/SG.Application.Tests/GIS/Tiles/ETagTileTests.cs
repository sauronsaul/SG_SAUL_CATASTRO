using FluentAssertions;
using SG.Application.GIS.Tiles;

namespace SG.Application.Tests.GIS.Tiles;

public sealed class ETagTileTests
{
    [Fact]
    public void Calcular_MismaEntrada_RetornaMismoETagFuerte()
    {
        var versionId = Guid.Parse("414792e5-dd1e-4863-b71f-59dc94e6b54c");

        var primero = ETagTile.Calcular(versionId, CapaTile.Parcelas, 16, 20602, 36574);
        var segundo = ETagTile.Calcular(versionId, CapaTile.Parcelas, 16, 20602, 36574);

        primero.Should().Be(segundo);
        primero.Should().MatchRegex("^\"[0-9a-f]{64}\"$");
    }

    [Fact]
    public void Calcular_CambiaVersion_CambiaETag()
    {
        var primero = ETagTile.Calcular(Guid.NewGuid(), CapaTile.Parcelas, 16, 20602, 36574);
        var segundo = ETagTile.Calcular(Guid.NewGuid(), CapaTile.Parcelas, 16, 20602, 36574);

        primero.Should().NotBe(segundo);
    }

    [Theory]
    [InlineData(CapaTile.Edificaciones, 16, 20602, 36574)]
    [InlineData(CapaTile.Parcelas, 17, 20602, 36574)]
    [InlineData(CapaTile.Parcelas, 16, 20603, 36574)]
    [InlineData(CapaTile.Parcelas, 16, 20602, 36575)]
    public void Calcular_CambiaComponenteDeRuta_CambiaETag(CapaTile capa, int z, int x, int y)
    {
        var versionId = Guid.Parse("414792e5-dd1e-4863-b71f-59dc94e6b54c");
        var baseEtag = ETagTile.Calcular(versionId, CapaTile.Parcelas, 16, 20602, 36574);

        ETagTile.Calcular(versionId, capa, z, x, y).Should().NotBe(baseEtag);
    }
}
