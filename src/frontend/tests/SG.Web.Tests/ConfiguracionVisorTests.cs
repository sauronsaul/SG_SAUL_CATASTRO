using FluentAssertions;
using SG.Contracts.GIS;

namespace SG.Web.Tests;

public sealed class ConfiguracionVisorTests
{
    [Fact]
    public void ContratoConservaBboxMunicipalNombrado()
    {
        var limites = new LimitesVisorDto(-66.84, -20.48, -66.80, -20.44);

        new[] { limites.Oeste, limites.Sur, limites.Este, limites.Norte }
            .Should().Equal(-66.84, -20.48, -66.80, -20.44);
    }
}
