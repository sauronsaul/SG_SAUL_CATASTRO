using FluentAssertions;
using SG.Application.GIS.Tiles;
using SG.Domain.Importacion;

namespace SG.Application.Tests.GIS.Tiles;

public sealed class ETagTileTests
{
    [Fact]
    public void Calcular_MismaEntrada_RetornaMismoETagFuerte()
    {
        var versionId = Guid.Parse("414792e5-dd1e-4863-b71f-59dc94e6b54c");
        var primero = ETagTile.Calcular("051201", versionId, 3, TipoCapa.Predios, 16, 20602, 36574);
        var segundo = ETagTile.Calcular("051201", versionId, 3, TipoCapa.Predios, 16, 20602, 36574);

        primero.Should().Be(segundo);
        primero.Should().MatchRegex("^\"[0-9a-f]{64}\"$");
    }

    [Theory]
    [InlineData("022001", 3, TipoCapa.Predios, 16, 20602, 36574)]
    [InlineData("051201", 4, TipoCapa.Predios, 16, 20602, 36574)]
    [InlineData("051201", 3, TipoCapa.Construcciones, 16, 20602, 36574)]
    [InlineData("051201", 3, TipoCapa.Predios, 17, 20602, 36574)]
    [InlineData("051201", 3, TipoCapa.Predios, 16, 20603, 36574)]
    [InlineData("051201", 3, TipoCapa.Predios, 16, 20602, 36575)]
    public void Calcular_CambiaComponente_CambiaETag(
        string municipio,
        int numeroVersion,
        TipoCapa capa,
        int z,
        int x,
        int y)
    {
        var versionId = Guid.Parse("414792e5-dd1e-4863-b71f-59dc94e6b54c");
        var baseEtag = ETagTile.Calcular("051201", versionId, 3, TipoCapa.Predios, 16, 20602, 36574);

        ETagTile.Calcular(municipio, versionId, numeroVersion, capa, z, x, y)
            .Should().NotBe(baseEtag);
    }
}
