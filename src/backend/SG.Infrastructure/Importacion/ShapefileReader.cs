using System.Globalization;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Esri;
using NetTopologySuite.IO.Esri.Dbf;
using NetTopologySuite.IO.Esri.Shapefiles.Readers;
using ProjNet.CoordinateSystems;
using ProjNet.CoordinateSystems.Transformations;
using SG.Application.Abstractions;

namespace SG.Infrastructure.Importacion;

// EPSG:32719 — UTM WGS84 Zona 19 Sur (SRID base del sistema, ver CLAUDE.md y ADR 0030).
internal sealed class ShapefileReader : IShapefileReader
{
    private const long SridDestino = 32719;

    private static readonly CoordinateSystemFactory CsFactory = new();
    private static readonly CoordinateTransformationFactory TransformFactory = new();

    public IEnumerable<RegistroCrudoShapefile> Leer(string rutaShp)
    {
        var (transformacion, prjWkt, proyeccionDesconocida) = LeerProyeccion(rutaShp);
        var rutaDbf = Path.ChangeExtension(rutaShp, ".dbf");
        var rutaShx = Path.ChangeExtension(rutaShp, ".shx");

        using var dbfReader = new DbfReader(rutaDbf);
        using var shpReader = Shapefile.OpenRead(rutaShp);
        using var shpReaderTolerante = Shapefile.OpenRead(rutaShp, new ShapefileReaderOptions
        {
            GeometryBuilderMode = GeometryBuilderMode.IgnoreInvalidShapes,
        });

        // Pre-verificación: registros DBF y SHP deben coincidir exactamente.
        // Una diferencia indica shapefile corrupto/desincronizado — abortar antes de leer.
        int dbfTotal = dbfReader.RecordCount;
        int shpTotal = ContarRegistrosSHP(rutaShx);
        if (dbfTotal != shpTotal)
            throw new InvalidOperationException(
                $"Shapefile corrupto: DBF tiene {dbfTotal} registros y SHP tiene {shpTotal} " +
                $"en '{Path.GetFileName(rutaShp)}'. Importación abortada.");

        while (true)
        {
            bool dbfHasNext = dbfReader.Read(out IAttributesTable? attrs, out _);
            if (!dbfHasNext) break;

            var atributos = BuildAtributos(attrs);
            Geometry? geometria = null;
            string? errorGeometria = null;

            // El lector tolerante avanza en paralelo, pero solo aporta su geometría
            // cuando el lector estricto no logra construir un shape con bytes.
            bool shpToleranteHasNext = shpReaderTolerante.Read(out _);

            // Read() avanza el lector SHP y parsea la geometría en un único paso de la API.
            // IOException se propaga (error de stream/archivo roto, no recuperable).
            // Cualquier otro fallo de construcción habilita el fallback tolerante.
            // shpHasNext permanece false si Read() lanza; en ese caso el catch lo corrige.
            bool shpHasNext = false;
            try
            {
                shpHasNext = shpReader.Read(out _);
                if (shpHasNext)
                {
                    geometria = shpReader.Geometry;
                    // NetTopologySuite representa los registros SHP Null Shape como una
                    // geometría tipada vacía. Desde este límite de lectura se normalizan
                    // a null para conservar la semántica del archivo de origen.
                    if (geometria?.IsEmpty == true)
                        geometria = null;
                    if (geometria is not null)
                    {
                        if (transformacion is not null)
                            geometria = AplicarTransformacion(geometria, transformacion);
                        else if (!proyeccionDesconocida)
                            geometria.SRID = (int)SridDestino;
                    }
                }
            }
            catch (IOException ex) when (ex is not ShapefileException) { throw; }
            catch (Exception ex)
            {
                // Read() lanzó al parsear la geometría de un registro que sí existe.
                // ShapefileException hereda de IOException — el filtro anterior la deja
                // pasar aquí en lugar de relanzarla como error de stream. Los Null Shape
                // genuinos no lanzan: el lector estricto los devuelve como geometría vacía.
                shpHasNext = true;
                geometria = shpReaderTolerante.Geometry;
                if (geometria is null || geometria.IsEmpty)
                {
                    geometria = null;
                    var msg = ex.Message;
                    errorGeometria = msg.Length > 200 ? msg[..200] : msg;
                }
                else if (transformacion is not null)
                {
                    geometria = AplicarTransformacion(geometria, transformacion);
                }
                else if (!proyeccionDesconocida)
                {
                    geometria.SRID = (int)SridDestino;
                }
            }

            // Detección de desincronización en tiempo de lectura (defensa en profundidad:
            // la pre-verificación de conteos ya debería haber evitado llegar aquí).
            if (!shpHasNext || !shpToleranteHasNext)
                throw new InvalidOperationException(
                    $"Shapefile desincronizado durante lectura: SHP terminó antes que DBF " +
                    $"en '{Path.GetFileName(rutaShp)}'. Archivo probablemente corrupto.");

            yield return new RegistroCrudoShapefile(
                geometria, atributos, proyeccionDesconocida, prjWkt, errorGeometria);
        }
    }

    private static int ContarRegistrosSHP(string rutaShx)
    {
        // Formato SHX: 100 bytes de cabecera fija + 8 bytes por registro.
        return (int)((new FileInfo(rutaShx).Length - 100) / 8);
    }

    private static (ICoordinateTransformation?, string?, bool proyeccionDesconocida) LeerProyeccion(string rutaShp)
    {
        var rutaPrj = Path.ChangeExtension(rutaShp, ".prj");
        if (!File.Exists(rutaPrj))
            return (null, null, true);

        var wkt = File.ReadAllText(rutaPrj).Trim();
        if (string.IsNullOrEmpty(wkt))
            return (null, null, true);

        try
        {
            var csOrigen = CsFactory.CreateFromWkt(wkt);
            var csDestino = ProjectedCoordinateSystem.WGS84_UTM(19, false);

            if (csOrigen.AuthorityCode == SridDestino)
                return (null, wkt, false);

            var transformacion = TransformFactory.CreateFromCoordinateSystems(csOrigen, csDestino);
            return (transformacion, wkt, false);
        }
        catch
        {
            return (null, wkt, true);
        }
    }

    private static Geometry AplicarTransformacion(Geometry geometria, ICoordinateTransformation transformacion)
    {
        var math = transformacion.MathTransform;
        var copia = (Geometry)geometria.Copy();
        copia.Apply(new MathTransformFilter(math));
        copia.GeometryChanged(); // invalida envelope cacheado por Copy() antes de la mutación
        copia.SRID = (int)SridDestino;
        return copia;
    }

    private static Dictionary<string, object?> BuildAtributos(IAttributesTable? attrs)
    {
        if (attrs is null)
            return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in attrs.GetNames())
            dict[name] = attrs[name];
        return dict;
    }
}

// Adapta MathTransform al ICoordinateFilter de NTS para transformar coordenadas in-place.
file sealed class MathTransformFilter(MathTransform transform) : ICoordinateFilter
{
    public void Filter(Coordinate coord)
    {
        double[] resultado = transform.Transform([coord.X, coord.Y]);
        coord.X = resultado[0];
        coord.Y = resultado[1];
    }
}
