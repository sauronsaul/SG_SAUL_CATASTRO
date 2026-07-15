using FluentAssertions;
using SG.Web.Models;
using SG.Web.Services;

namespace SG.Web.Tests;

public sealed class CatalogoCapasMapaTests
{
    [Fact]
    public void TieneLasSieteCapasPermitidas()
    {
        CatalogoCapasMapa.ObtenerTodas().Select(x => x.Nombre).Should().BeEquivalentTo(
            "parcelas",
            "edificaciones",
            "predios-no-fotografiados",
            "manzanas",
            "distritos",
            "zonas",
            "vias");
    }

    [Fact]
    public void NoAceptaUnaCapaFueraDelCatalogo()
    {
        CatalogoCapasMapa.IntentarObtener("usuarios;drop table", out var capa).Should().BeFalse();
        capa.Should().BeNull();
    }

    [Fact]
    public void OrdenaRellenosAntesDeLineasYEtiquetas()
    {
        var tipos = CatalogoCapasMapa.ObtenerOrdenDibujo().Select(x => (int)x.Tipo);

        tipos.Should().BeInAscendingOrder();
    }

    [Fact]
    public void ParcelasYEdificacionesNoSeSolicitanEnZoomBajo()
    {
        CatalogoCapasMapa.IntentarObtener("parcelas", out var parcelas).Should().BeTrue();
        CatalogoCapasMapa.IntentarObtener("edificaciones", out var edificaciones).Should().BeTrue();

        parcelas!.MinZoom.Should().BeGreaterThanOrEqualTo(15);
        edificaciones!.MinZoom.Should().BeGreaterThanOrEqualTo(16);
    }
}
