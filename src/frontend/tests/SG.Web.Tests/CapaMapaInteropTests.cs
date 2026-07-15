using System.Text.Json;
using FluentAssertions;
using SG.Web.Models;
using SG.Web.Services;

namespace SG.Web.Tests;

public sealed class CapaMapaInteropTests
{
    [Fact]
    public void ProyectaLasSieteCapasConContratoJsonExplicito()
    {
        var capas = CatalogoCapasMapa.ObtenerTodas()
            .Select(capa => CapaMapaInterop.Crear(capa, visible: true))
            .ToArray();

        var json = JsonSerializer.Serialize(capas);
        using var documento = JsonDocument.Parse(json);

        capas.Should().HaveCount(7);
        capas.Select(x => x.Nombre).Should().OnlyHaveUniqueItems();
        documento.RootElement.GetArrayLength().Should().Be(7);
        documento.RootElement[0].TryGetProperty("nombre", out _).Should().BeTrue();
        documento.RootElement[0].TryGetProperty("minZoom", out _).Should().BeTrue();
        documento.RootElement[0].TryGetProperty("Nombre", out _).Should().BeFalse();
    }
}
