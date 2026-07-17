using System.Text.Json;
using FluentAssertions;
using SG.Contracts.GIS;
using SG.Web.Models;

namespace SG.Web.Tests;

public sealed class CapaMapaInteropTests
{
    [Fact]
    public void ProyectaContratoDataDrivenConCirculo()
    {
        var capa = new CapaVisorDto(
            "PuntosGeodesicos", "puntos-geodesicos", "Puntos geodésicos",
            90, 11, "#DC2626", false, false, true, "puntos", 13);

        var interop = CapaMapaInterop.Crear(capa, true);
        var json = JsonSerializer.Serialize(interop);
        using var documento = JsonDocument.Parse(json);

        documento.RootElement.GetProperty("nombre").GetString().Should().Be("puntos-geodesicos");
        documento.RootElement.GetProperty("tieneCirculo").GetBoolean().Should().BeTrue();
        documento.RootElement.TryGetProperty("Nombre", out _).Should().BeFalse();
    }
}
