using FluentAssertions;
using NSubstitute;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catastro;
using SG.Application.Abstractions.Importacion;
using SG.Application.Importacion.Confirmar;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Application.Tests.Importacion;

public sealed class ConfirmarImportacionHandlerTests
{
    private readonly IImportacionRepositorio _importaciones = Substitute.For<IImportacionRepositorio>();
    private readonly IPerfilImportacionRepositorio _perfiles = Substitute.For<IPerfilImportacionRepositorio>();
    private readonly IPredioRepositorio _predios = Substitute.For<IPredioRepositorio>();
    private readonly IShapefileReader _shapefileReader = Substitute.For<IShapefileReader>();
    private readonly IZipExtractor _zipExtractor = Substitute.For<IZipExtractor>();
    private readonly IMapeadorImportacion _mapeador = Substitute.For<IMapeadorImportacion>();
    private readonly IMinioService _minio = Substitute.For<IMinioService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();

    private ConfirmarImportacionHandler CrearHandler() =>
        new(_importaciones, _perfiles, _predios, _shapefileReader, _zipExtractor, _mapeador, _minio, _currentUser);

    [Fact]
    public async Task Handle_ImportacionNoEncontrada_RetornaNoEncontrada()
    {
        _importaciones
            .ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((ImportacionDomain.Importacion?)null);

        var result = await CrearHandler().Handle(
            new ConfirmarImportacionCommand(Guid.NewGuid()),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ImportacionDomain.ImportacionErrores.NoEncontrada);
    }

    [Fact]
    public async Task Handle_ImportacionYaConfirmada_RetornaYaConfirmada_SinReprocesar()
    {
        var importacion = ImportacionDomain.Importacion.CrearPreview(
            Guid.NewGuid(), "test.zip", "importaciones/abc.zip", Guid.NewGuid(), 50);
        importacion.Confirmar();

        _importaciones
            .ObtenerPorIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(importacion);

        var result = await CrearHandler().Handle(
            new ConfirmarImportacionCommand(importacion.Id),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ImportacionDomain.ImportacionErrores.YaConfirmada);
        await _minio.DidNotReceive().DescargarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
