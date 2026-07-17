using FluentAssertions;
using NSubstitute;
using SG.Application.Abstractions.Catalogos;
using SG.Application.Abstractions.GIS;
using SG.Application.Abstractions.Importacion;
using SG.Application.GIS.Visor;
using SG.Contracts.GIS;
using SG.Domain.Catalogos;
using SG.Domain.Importacion;

namespace SG.Application.Tests.GIS.Visor;

public sealed class ObtenerConfiguracionVisorQueryHandlerTests
{
    private readonly IMunicipioRepositorio _municipios = Substitute.For<IMunicipioRepositorio>();
    private readonly IDatasetVersionRepositorio _versiones = Substitute.For<IDatasetVersionRepositorio>();
    private readonly IEsquemaCapasMunicipioRepositorio _esquemas =
        Substitute.For<IEsquemaCapasMunicipioRepositorio>();
    private readonly IExtensionMunicipalService _extension =
        Substitute.For<IExtensionMunicipalService>();

    [Fact]
    public async Task Handle_CodigoInvalido_Retorna400ContractualSinConsultar()
    {
        var resultado = await CrearHandler().Handle(new("UYUNI"), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(ErroresVisor.MunicipioCodigoInvalido);
        await _municipios.DidNotReceive()
            .ObtenerPorCodigoIneAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SinDatasetActivo_RetornaConflictoContractual()
    {
        _municipios.ObtenerPorCodigoIneAsync("051201", Arg.Any<CancellationToken>())
            .Returns(CrearMunicipio());
        _versiones.ObtenerActivaAsync("051201", Arg.Any<CancellationToken>())
            .Returns((DatasetVersion?)null);

        var resultado = await CrearHandler().Handle(new("051201"), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(ErroresVisor.DatasetActivoNoDisponible);
    }

    [Fact]
    public async Task Handle_ConfiguraSoloCapasDelEsquemaYCapacidadPredial()
    {
        var version = DatasetVersion.Crear(3, "051201", null, "dataset de prueba");
        version.MarcarPreviewListo();
        version.Activar(Guid.NewGuid());
        var esquema = new[]
        {
            EsquemaCapaMunicipio.Crear(
                "051201", TipoCapa.Predios, "predios", "predios.shp", "capa_parcelas", true).Value,
            EsquemaCapaMunicipio.Crear(
                "051201", TipoCapa.Vias, "vias", "vias.shp", "capa_vias", false).Value,
        };
        _municipios.ObtenerPorCodigoIneAsync("051201", Arg.Any<CancellationToken>())
            .Returns(CrearMunicipio());
        _versiones.ObtenerActivaAsync("051201", Arg.Any<CancellationToken>()).Returns(version);
        _esquemas.ListarAsync("051201", Arg.Any<CancellationToken>()).Returns(esquema);
        _extension.ObtenerAsync("051201", version.Id, Arg.Any<CancellationToken>())
            .Returns(new LimitesVisorDto(-66.9, -20.5, -66.8, -20.4));

        var resultado = await CrearHandler().Handle(new("051201"), CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Municipio.Nombre.Should().Be("Uyuni");
        resultado.Value.NumeroVersionActiva.Should().Be(3);
        resultado.Value.Capacidades.TienePredios.Should().BeTrue();
        resultado.Value.Capas.Select(x => x.Nombre).Should().Equal("parcelas", "vias");
    }

    [Fact]
    public async Task Handle_DatasetActivoSinGeometrias_Retorna422Contractual()
    {
        var version = DatasetVersion.Crear(1, "051201", null, "vacío");
        version.MarcarPreviewListo();
        version.Activar(Guid.NewGuid());
        _municipios.ObtenerPorCodigoIneAsync("051201", Arg.Any<CancellationToken>())
            .Returns(CrearMunicipio());
        _versiones.ObtenerActivaAsync("051201", Arg.Any<CancellationToken>()).Returns(version);
        _esquemas.ListarAsync("051201", Arg.Any<CancellationToken>()).Returns(
            [EsquemaCapaMunicipio.Crear(
                "051201", TipoCapa.Predios, "predios", "predios.shp", "capa_parcelas", true).Value]);
        _extension.ObtenerAsync("051201", version.Id, Arg.Any<CancellationToken>())
            .Returns((LimitesVisorDto?)null);

        var resultado = await CrearHandler().Handle(new("051201"), CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Should().Be(ErroresVisor.DatasetSinGeometrias);
    }

    private ObtenerConfiguracionVisorQueryHandler CrearHandler() =>
        new(_municipios, _versiones, _esquemas, _extension);

    private static Municipio CrearMunicipio() =>
        Municipio.Crear("051201", "Uyuni", "GAM Uyuni", "Potosí", "INE").Value;
}
