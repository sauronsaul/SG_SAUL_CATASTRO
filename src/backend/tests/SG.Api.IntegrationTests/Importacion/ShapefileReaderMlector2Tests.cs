using System.Buffers.Binary;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.Operation.Valid;
using ProjNet.CoordinateSystems;
using SG.Application.Abstractions;
using SG.Api.IntegrationTests.Infrastructure;
using SG.Domain.Importacion;
using SG.Infrastructure.Persistencia;

namespace SG.Api.IntegrationTests.Importacion;

[Collection("Postgres")]
public sealed class ShapefileReaderMlector2Tests : IDisposable
{
    private const string EsriWktUtm19Sur =
        "PROJCS[\"WGS_1984_UTM_Zone_19S\",GEOGCS[\"GCS_WGS_1984\",DATUM[\"D_WGS_1984\"," +
        "SPHEROID[\"WGS_1984\",6378137.0,298.257223563]],PRIMEM[\"Greenwich\",0.0]," +
        "UNIT[\"Degree\",0.0174532925199433]],PROJECTION[\"Transverse_Mercator\"]," +
        "PARAMETER[\"False_Easting\",500000.0],PARAMETER[\"False_Northing\",10000000.0]," +
        "PARAMETER[\"Central_Meridian\",-69.0],PARAMETER[\"Scale_Factor\",0.9996]," +
        "PARAMETER[\"Latitude_Of_Origin\",0.0],UNIT[\"Meter\",1.0]]";

    private readonly string _connectionString;
    private readonly SgApiFactory _factory;
    private readonly IShapefileReader _reader;

    public ShapefileReaderMlector2Tests(PostgreSqlFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        _factory = new SgApiFactory(_connectionString);
        _reader = _factory.Services.GetRequiredService<IShapefileReader>();
    }

    [Fact]
    public void Leer_AnilloAbierto_CierraConUnaCopiaExactaDelPrimerPunto()
    {
        using var archivo = ShapefilePrueba.Crear(
            CrearCuadrado(),
            EsriWktUtm19Sur,
            ultimoPuntoCrudo: new Coordinate(600005, 8200005));

        var fuente = archivo.LeerPuntosCrudos();
        var registro = _reader.Leer(archivo.RutaShp).Single();

        registro.ErrorGeometria.Should().BeNull();
        var anillo = ObtenerExterior(registro.Geometria);
        anillo.IsClosed.Should().BeTrue();
        anillo.Coordinates.Should().HaveCount(fuente.Count + 1);
        anillo.Coordinates.Take(fuente.Count).Should().Equal(fuente, CoordenadasIguales);
        CoordenadasIguales(anillo.Coordinates[^1], fuente[0]).Should().BeTrue();
        registro.Geometria!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Leer_AnilloAbiertoAutoIntersectado_PersisteLaInvalidezRealSinCentinela()
    {
        using var archivo = ShapefilePrueba.Crear(
            CrearLazo(),
            EsriWktUtm19Sur,
            ultimoPuntoCrudo: new Coordinate(600005, 8200000));

        var registro = _reader.Leer(archivo.RutaShp).Single();
        var geometria = registro.Geometria.Should().BeOfType<MultiPolygon>().Subject;
        var motivoNts = new IsValidOp(geometria).ValidationError?.Message;

        registro.ErrorGeometria.Should().BeNull();
        geometria.IsValid.Should().BeFalse();
        motivoNts.Should().Contain("Self-intersection");
        EsCentinela(geometria).Should().BeFalse();

        await using var db = CrearContextoSinInterceptors();
        await using var transaccion = await db.Database.BeginTransactionAsync();
        var numeroVersion =
            (await db.DatasetVersiones
                .Where(x => x.MunicipioCodigo == "051201")
                .MaxAsync(x => (int?)x.NumeroVersion) ?? 0) + 1;
        var version = DatasetVersion.Crear(numeroVersion, "051201", null, "Prueba M-LECTOR-2");
        db.DatasetVersiones.Add(version);
        await db.SaveChangesAsync();

        var manzana = CapaManzana.Crear(version.Id, geometria, "{}", 1, "M", 1, 1, null);
        db.CapasManzanas.Add(manzana);
        await db.SaveChangesAsync();

        var motivoPostgis = await db.Database.SqlQuery<string>(
                $"SELECT ST_IsValidReason(geometria) AS \"Value\" FROM dominio.capa_manzanas WHERE id = {manzana.Id}")
            .SingleAsync();
        motivoPostgis.Should().Contain("Self-intersection");
        motivoPostgis.Should().NotContain("Too few points");
        await transaccion.RollbackAsync();
    }

    [Fact]
    public void Leer_AnilloYaCerradoAutoIntersectado_ConservaCoordenadasEInvalidez()
    {
        using var archivo = ShapefilePrueba.Crear(CrearLazo(), EsriWktUtm19Sur);
        var fuente = archivo.LeerPuntosCrudos();

        var registro = _reader.Leer(archivo.RutaShp).Single();
        var geometria = registro.Geometria.Should().BeOfType<MultiPolygon>().Subject;
        var anillo = ObtenerExterior(geometria);

        registro.ErrorGeometria.Should().BeNull();
        geometria.IsValid.Should().BeFalse();
        new IsValidOp(geometria).ValidationError?.Message.Should().Contain("Self-intersection");
        anillo.Coordinates.Should().Equal(fuente, CoordenadasIguales);
    }

    [Fact]
    public void Leer_PrjEsriUtm19SurSinAuthority_AsignaSridSinTransformarCoordenadas()
    {
        using var archivo = ShapefilePrueba.Crear(CrearCuadrado(), EsriWktUtm19Sur);
        var fuente = archivo.LeerPuntosCrudos();

        var registro = _reader.Leer(archivo.RutaShp).Single();
        var geometria = registro.Geometria.Should().BeOfType<MultiPolygon>().Subject;

        geometria.SRID.Should().Be(32719);
        ObtenerExterior(geometria).Coordinates.Should().Equal(fuente, CoordenadasIguales);
        registro.ProyeccionDesconocida.Should().BeFalse();
    }

    [Fact]
    public void Leer_PrjUtm18Sur_ConservaLaTransformacionHaciaUtm19Sur()
    {
        using var archivo = ShapefilePrueba.Crear(
            CrearCuadrado(),
            ProjectedCoordinateSystem.WGS84_UTM(18, false).WKT);
        var fuente = archivo.LeerPuntosCrudos();

        var registro = _reader.Leer(archivo.RutaShp).Single();
        var geometria = registro.Geometria.Should().BeOfType<MultiPolygon>().Subject;
        var salida = ObtenerExterior(geometria).Coordinates;

        geometria.SRID.Should().Be(32719);
        registro.ProyeccionDesconocida.Should().BeFalse();
        salida[0].X.Should().NotBe(fuente[0].X);
        salida[0].Y.Should().NotBe(fuente[0].Y);
    }

    [Fact]
    public void Leer_FallbackToleranteProduceCentinela_DevuelveNullConError()
    {
        using var archivo = ShapefilePrueba.Crear(CrearCuadrado(), EsriWktUtm19Sur);
        archivo.SobrescribirCantidadPuntos(2);

        var registro = _reader.Leer(archivo.RutaShp).Single();

        registro.Geometria.Should().BeNull();
        registro.ErrorGeometria.Should().Contain("centinela de anillo inv");
    }

    public void Dispose() => _factory.Dispose();

    private ApplicationDbContext CrearContextoSinInterceptors()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_connectionString, npgsql => npgsql.UseNetTopologySuite())
            .UseSnakeCaseNamingConvention()
            .Options;
        return new ApplicationDbContext(options);
    }

    private static LinearRing ObtenerExterior(Geometry? geometria) =>
        (LinearRing)((MultiPolygon)geometria!).GetGeometryN(0).Boundary;

    private static bool CoordenadasIguales(Coordinate izquierda, Coordinate derecha) =>
        BitConverter.DoubleToInt64Bits(izquierda.X) == BitConverter.DoubleToInt64Bits(derecha.X) &&
        BitConverter.DoubleToInt64Bits(izquierda.Y) == BitConverter.DoubleToInt64Bits(derecha.Y);

    private static bool EsCentinela(Geometry geometria) =>
        geometria.NumPoints == 4 && geometria.Coordinates.Skip(1).All(x => x.Equals2D(geometria.Coordinate));

    private static Polygon CrearCuadrado() => new(new LinearRing(
    [
        new Coordinate(600000, 8200000),
        new Coordinate(600010, 8200000),
        new Coordinate(600010, 8200010),
        new Coordinate(600000, 8200010),
        new Coordinate(600000, 8200000),
    ]));

    private static Polygon CrearLazo() => new(new LinearRing(
    [
        new Coordinate(600000, 8200000),
        new Coordinate(600010, 8200010),
        new Coordinate(600000, 8200010),
        new Coordinate(600010, 8200000),
        new Coordinate(600000, 8200000),
    ]));

    private sealed class ShapefilePrueba : IDisposable
    {
        private const int CabeceraShxBytes = 100;
        private readonly string _directorio;

        private ShapefilePrueba(string directorio, string rutaShp)
        {
            _directorio = directorio;
            RutaShp = rutaShp;
        }

        public string RutaShp { get; }

        public static ShapefilePrueba Crear(
            Polygon poligono,
            string prj,
            Coordinate? ultimoPuntoCrudo = null)
        {
            var directorio = Path.Combine(Path.GetTempPath(), $"sg-mlector2-{Guid.NewGuid():N}");
            Directory.CreateDirectory(directorio);
            var rutaShp = Path.Combine(directorio, "MANZANOS_PROY.shp");
            Shapefile.WriteAllFeatures(
                [new Feature(poligono, new AttributesTable { { "ID", 1L } })],
                rutaShp);
            File.WriteAllText(Path.ChangeExtension(rutaShp, ".prj"), prj);

            var archivo = new ShapefilePrueba(directorio, rutaShp);
            if (ultimoPuntoCrudo is not null)
                archivo.SobrescribirUltimoPunto(ultimoPuntoCrudo);
            return archivo;
        }

        public List<Coordinate> LeerPuntosCrudos()
        {
            var inicioContenido = ObtenerInicioContenido();
            using var stream = File.OpenRead(RutaShp);
            using var reader = new BinaryReader(stream);
            stream.Position = inicioContenido + 36;
            var cantidadPartes = reader.ReadInt32();
            var cantidadPuntos = reader.ReadInt32();
            stream.Position += cantidadPartes * 4L;
            var puntos = new List<Coordinate>(cantidadPuntos);
            for (var i = 0; i < cantidadPuntos; i++)
                puntos.Add(new Coordinate(reader.ReadDouble(), reader.ReadDouble()));
            return puntos;
        }

        public void SobrescribirCantidadPuntos(int cantidad)
        {
            using var stream = File.Open(RutaShp, FileMode.Open, FileAccess.Write, FileShare.None);
            stream.Position = ObtenerInicioContenido() + 40;
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bytes, cantidad);
            stream.Write(bytes);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directorio))
                Directory.Delete(_directorio, recursive: true);
        }

        private void SobrescribirUltimoPunto(Coordinate punto)
        {
            var inicioContenido = ObtenerInicioContenido();
            using var stream = File.Open(RutaShp, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            stream.Position = inicioContenido + 36;
            var cantidadPartes = reader.ReadInt32();
            var cantidadPuntos = reader.ReadInt32();
            stream.Position = inicioContenido + 44 + cantidadPartes * 4L + (cantidadPuntos - 1) * 16L;
            using var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, leaveOpen: true);
            writer.Write(punto.X);
            writer.Write(punto.Y);
        }

        private long ObtenerInicioContenido()
        {
            using var stream = File.OpenRead(Path.ChangeExtension(RutaShp, ".shx"));
            stream.Position = CabeceraShxBytes;
            Span<byte> bytes = stackalloc byte[4];
            stream.ReadExactly(bytes);
            return BinaryPrimitives.ReadInt32BigEndian(bytes) * 2L + 8;
        }
    }
}
