using FluentAssertions;
using NSubstitute;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catalogos;
using SG.Application.Abstractions.Importacion;
using SG.Application.Importacion.Versiones;
using SG.Domain.Importacion;

namespace SG.Application.Tests.Importacion;

public sealed class CrearVersionImportacionHandlerTests
{
    private readonly IPerfilImportacionRepositorio _perfiles = Substitute.For<IPerfilImportacionRepositorio>();
    private readonly IDatasetVersionRepositorio _versiones = Substitute.For<IDatasetVersionRepositorio>();
    private readonly IMinioService _minio = Substitute.For<IMinioService>();
    private readonly IColaCargaVersionada _cola = Substitute.For<IColaCargaVersionada>();
    private readonly IEsquemaCapasMunicipioRepositorio _esquemas = Substitute.For<IEsquemaCapasMunicipioRepositorio>();
    private readonly IMunicipioRepositorio _municipios = Substitute.For<IMunicipioRepositorio>();
    private readonly IInspectorPaqueteVersionado _inspector = Substitute.For<IInspectorPaqueteVersionado>();

    [Fact]
    public async Task Handle_CodigoIneMalformado_Retorna400DeDominioSinEfectosLaterales()
    {
        var resultado = await CrearHandler().Handle(
            CrearCommand("22001"),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be("Municipio.CodigoIneInvalido");
        await _minio.DidNotReceiveWithAnyArgs().SubirAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Handle_MunicipioInexistente_RetornaErrorClaroSinEfectosLaterales()
    {
        _municipios.ExistePorCodigoIneAsync("999999", Arg.Any<CancellationToken>()).Returns(false);

        var resultado = await CrearHandler().Handle(
            CrearCommand("999999"),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be("VersionImportacion.MunicipioNoEncontrado");
        await _minio.DidNotReceiveWithAnyArgs().SubirAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Handle_MunicipioSinEsquema_RetornaErrorClaroSinEfectosLaterales()
    {
        _municipios.ExistePorCodigoIneAsync("022001", Arg.Any<CancellationToken>()).Returns(true);
        _esquemas.ListarAsync("022001", Arg.Any<CancellationToken>())
            .Returns(Array.Empty<EsquemaCapaMunicipio>());

        var resultado = await CrearHandler().Handle(
            CrearCommand("022001"),
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error.Code.Should().Be("VersionImportacion.EsquemaMunicipalNoConfigurado");
        await _minio.DidNotReceiveWithAnyArgs().SubirAsync(default!, default!, default!, default, default);
    }

    [Fact]
    public async Task Handle_MunicipioExplicito_CreaVersionConSuEsquema()
    {
        var definicion = EsquemaCapaMunicipio.Crear(
            "022001",
            TipoCapa.Manzanas,
            "caranavi-versionado-manzanas",
            "MANZANOS_PROY.shp",
            "capa_manzanas",
            true).Value;
        var perfil = PerfilImportacion.Crear(
            definicion.NombrePerfil,
            definicion.TipoCapa,
            definicion.NombreArchivoShp);
        DatasetVersion? agregada = null;

        _municipios.ExistePorCodigoIneAsync("022001", Arg.Any<CancellationToken>()).Returns(true);
        _esquemas.ListarAsync("022001", Arg.Any<CancellationToken>()).Returns([definicion]);
        _perfiles.ListarAsync(Arg.Any<CancellationToken>()).Returns([perfil]);
        _inspector.Inspeccionar(Arg.Any<Stream>(), Arg.Any<IReadOnlyList<EsquemaCapaMunicipio>>())
            .Returns(new ResultadoInspeccionPaqueteVersionado(
                true,
                new HashSet<string>([definicion.NombrePerfil], StringComparer.Ordinal),
                []));
        _versiones.ObtenerSiguienteNumeroAsync("022001", Arg.Any<CancellationToken>()).Returns(4);
        _versiones.When(x => x.Agregar(Arg.Any<DatasetVersion>()))
            .Do(x => agregada = x.Arg<DatasetVersion>());
        _minio.SubirAsync(Arg.Any<Stream>(), Arg.Any<string>(), "application/zip", Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns("objeto");

        var resultado = await CrearHandler().Handle(
            CrearCommand("022001"),
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        agregada.Should().NotBeNull();
        agregada!.MunicipioCodigo.Should().Be("022001");
        agregada.NumeroVersion.Should().Be(4);
        agregada.OrigenDescripcion.Should().Be("Paquete de 1 capa: prueba.zip");
        await _versiones.Received(1).ObtenerSiguienteNumeroAsync("022001", Arg.Any<CancellationToken>());
        await _cola.Received(1).EncolarAsync(agregada.Id, Arg.Any<CancellationToken>());
    }

    private CrearVersionImportacionHandler CrearHandler() => new(
        _perfiles,
        _versiones,
        _minio,
        _cola,
        _esquemas,
        _municipios,
        _inspector);

    private static CrearVersionImportacionCommand CrearCommand(string municipioCodigo) => new(
        municipioCodigo,
        "prueba.zip",
        new MemoryStream([1, 2, 3]),
        3);
}
