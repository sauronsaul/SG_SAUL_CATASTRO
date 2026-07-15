using FluentAssertions;
using SG.Web.Models;

namespace SG.Web.Tests;

public sealed class ConfiguracionVisorTests
{
    [Fact]
    public void EntregaElBboxConfiguradoAlInteropSinAlterarlo()
    {
        var limitesConfigurados = new[]
        {
            -66.8474715001568,
            -20.48223928393051,
            -66.80351074623223,
            -20.440762937955693,
        };
        var configuracion = new ConfiguracionVisor("UYUNI", limitesConfigurados);

        var limitesInterop = configuracion.ObtenerLimitesMapa();

        limitesInterop.Should().Equal(limitesConfigurados);
    }
}
