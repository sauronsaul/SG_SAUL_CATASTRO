using System.Buffers.Binary;
using NetTopologySuite;
using NetTopologySuite.Geometries;

namespace SG.Infrastructure.Importacion;

internal sealed class ShpPolygonFallbackReader
{
    private const int CabeceraShxBytes = 100;
    private const int RegistroShxBytes = 8;
    private const int CabeceraRegistroShpBytes = 8;
    private const int TipoPolygon = 5;
    private const int TipoPolygonZ = 15;
    private const int TipoPolygonM = 25;

    private readonly GeometryFactory _geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory();

    public MultiPolygon Reconstruir(string rutaShp, string rutaShx, int indiceCeroBased)
    {
        var (offsetRegistro, longitudContenido) = LeerIndice(rutaShx, indiceCeroBased);
        using var stream = File.OpenRead(rutaShp);
        if (offsetRegistro < CabeceraShxBytes || offsetRegistro + CabeceraRegistroShpBytes > stream.Length)
            throw new InvalidDataException($"Offset SHX fuera del archivo para el registro {indiceCeroBased + 1}.");

        stream.Position = offsetRegistro + CabeceraRegistroShpBytes;
        using var reader = new BinaryReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
        var finContenido = stream.Position + longitudContenido;
        if (finContenido > stream.Length)
            throw new InvalidDataException($"Longitud SHP fuera del archivo para el registro {indiceCeroBased + 1}.");

        var tipo = reader.ReadInt32();
        if (tipo is not (TipoPolygon or TipoPolygonZ or TipoPolygonM))
            throw new NotSupportedException($"El registro SHP no es poligonal (ShapeType={tipo}).");

        AsegurarDisponible(stream, finContenido, 40);
        stream.Position += 32; // bounding box XY
        var cantidadPartes = reader.ReadInt32();
        var cantidadPuntos = reader.ReadInt32();
        if (cantidadPartes <= 0 || cantidadPuntos <= 0 || cantidadPartes > cantidadPuntos)
            throw new InvalidDataException(
                $"Conteos poligonales inválidos: partes={cantidadPartes}, puntos={cantidadPuntos}.");

        AsegurarDisponible(stream, finContenido, checked(cantidadPartes * 4L + cantidadPuntos * 16L));
        var inicios = new int[cantidadPartes];
        for (var i = 0; i < cantidadPartes; i++)
            inicios[i] = reader.ReadInt32();
        ValidarInicios(inicios, cantidadPuntos);

        var puntos = new PuntoCrudo[cantidadPuntos];
        for (var i = 0; i < cantidadPuntos; i++)
            puntos[i] = new PuntoCrudo(reader.ReadDouble(), reader.ReadDouble());

        if (tipo == TipoPolygonZ)
            LeerZ(reader, stream, finContenido, puntos);
        if (tipo is TipoPolygonZ or TipoPolygonM)
            LeerMOpcional(reader, stream, finContenido, puntos);

        var anillos = new List<LinearRing>(cantidadPartes);
        for (var parte = 0; parte < cantidadPartes; parte++)
        {
            var inicio = inicios[parte];
            var fin = parte + 1 < cantidadPartes ? inicios[parte + 1] : cantidadPuntos;
            var coordenadas = puntos[inicio..fin].Select(CrearCoordenada).ToList();
            if (coordenadas.Count < 3)
                throw new InvalidDataException(
                    $"La parte {parte + 1} tiene {coordenadas.Count} puntos; no puede formar un anillo.");

            if (!coordenadas[0].Equals2D(coordenadas[^1]))
                coordenadas.Add(coordenadas[0].Copy());

            anillos.Add(_geometryFactory.CreateLinearRing(coordenadas.ToArray()));
        }

        return ConstruirMultiPoligono(anillos);
    }

    private static (long OffsetBytes, long LongitudContenidoBytes) LeerIndice(
        string rutaShx,
        int indiceCeroBased)
    {
        using var stream = File.OpenRead(rutaShx);
        var posicion = CabeceraShxBytes + indiceCeroBased * (long)RegistroShxBytes;
        if (posicion < CabeceraShxBytes || posicion + RegistroShxBytes > stream.Length)
            throw new InvalidDataException($"No existe índice SHX para el registro {indiceCeroBased + 1}.");

        stream.Position = posicion;
        Span<byte> bytes = stackalloc byte[RegistroShxBytes];
        stream.ReadExactly(bytes);
        var offsetPalabras = BinaryPrimitives.ReadInt32BigEndian(bytes[..4]);
        var longitudPalabras = BinaryPrimitives.ReadInt32BigEndian(bytes[4..]);
        if (offsetPalabras < 0 || longitudPalabras <= 0)
            throw new InvalidDataException($"Índice SHX inválido para el registro {indiceCeroBased + 1}.");

        return (offsetPalabras * 2L, longitudPalabras * 2L);
    }

    private static void ValidarInicios(int[] inicios, int cantidadPuntos)
    {
        if (inicios[0] != 0)
            throw new InvalidDataException("La primera parte SHP no comienza en el punto cero.");

        for (var i = 0; i < inicios.Length; i++)
        {
            if (inicios[i] < 0 || inicios[i] >= cantidadPuntos ||
                (i > 0 && inicios[i] <= inicios[i - 1]))
                throw new InvalidDataException($"Offset de parte SHP inválido en el índice {i}.");
        }
    }

    private static void LeerZ(
        BinaryReader reader,
        Stream stream,
        long finContenido,
        PuntoCrudo[] puntos)
    {
        AsegurarDisponible(stream, finContenido, 16 + puntos.Length * 8L);
        stream.Position += 16; // rango Z
        for (var i = 0; i < puntos.Length; i++)
            puntos[i] = puntos[i] with { Z = reader.ReadDouble() };
    }

    private static void LeerMOpcional(
        BinaryReader reader,
        Stream stream,
        long finContenido,
        PuntoCrudo[] puntos)
    {
        var bytesNecesarios = 16 + puntos.Length * 8L;
        if (finContenido - stream.Position < bytesNecesarios)
            return;

        stream.Position += 16; // rango M
        for (var i = 0; i < puntos.Length; i++)
            puntos[i] = puntos[i] with { M = reader.ReadDouble() };
    }

    private static Coordinate CrearCoordenada(PuntoCrudo punto) => (punto.Z, punto.M) switch
    {
        (not null, not null) => new CoordinateZM(punto.X, punto.Y, punto.Z.Value, punto.M.Value),
        (not null, null) => new CoordinateZ(punto.X, punto.Y, punto.Z.Value),
        (null, not null) => new CoordinateM(punto.X, punto.Y, punto.M.Value),
        _ => new Coordinate(punto.X, punto.Y),
    };

    private MultiPolygon ConstruirMultiPoligono(List<LinearRing> anillos)
    {
        var poligonos = new List<Polygon>();
        var huecos = new List<LinearRing>();
        var exterior = anillos[0];

        for (var i = 1; i < anillos.Count; i++)
        {
            var anillo = anillos[i];
            if (anillo.IsCCW)
            {
                huecos.Add(anillo);
                continue;
            }

            poligonos.Add(_geometryFactory.CreatePolygon(exterior, huecos.ToArray()));
            exterior = anillo;
            huecos.Clear();
        }

        poligonos.Add(_geometryFactory.CreatePolygon(exterior, huecos.ToArray()));
        return _geometryFactory.CreateMultiPolygon(poligonos.ToArray());
    }

    private static void AsegurarDisponible(Stream stream, long finContenido, long bytes)
    {
        if (bytes < 0 || stream.Position + bytes > finContenido)
            throw new InvalidDataException("El registro SHP terminó antes de completar la geometría declarada.");
    }

    private sealed record PuntoCrudo(double X, double Y, double? Z = null, double? M = null);
}
