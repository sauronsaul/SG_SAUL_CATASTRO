using NetTopologySuite.Geometries;
using SG.Domain.Common;

namespace SG.Domain.Importacion;

public abstract class CapaVersionada : Entity
{
    public Guid DatasetVersionId { get; protected set; }
    public string AtributosExtra { get; protected set; } = "{}";
    public int FilaOrigen { get; protected set; }

    protected CapaVersionada() { }
}

public sealed class CapaParcela : CapaVersionada
{
    public Polygon Geometria { get; private set; } = null!;
    public int CodUv { get; private set; }
    public int CodMan { get; private set; }
    public int CodPred { get; private set; }
    public string? CodigoGeografico { get; private set; }
    public decimal? Superficie { get; private set; }
    public int? ValuacionZonal { get; private set; }
    public string? TipoInmueble { get; private set; }
    public string? ServicioAlcantarillado { get; private set; }
    public string? ServicioAgua { get; private set; }
    public string? ServicioLuz { get; private set; }
    public string? ServicioTelefonia { get; private set; }
    public string? NombrePropietarioOrigen { get; private set; }
    public string? NombreVia { get; private set; }
    public string? DireccionBarrio { get; private set; }
    public string? DireccionUrbana { get; private set; }
    public string? UsoTerreno { get; private set; }
    public string? TopografiaTerreno { get; private set; }

    private CapaParcela() { }

    public static CapaParcela Crear(
        Guid datasetVersionId, Polygon geometria, int codUv, int codMan, int codPred,
        string atributosExtra, int filaOrigen, string? codigoGeografico, decimal? superficie,
        int? valuacionZonal, string? tipoInmueble, string? servicioAlcantarillado,
        string? servicioAgua, string? servicioLuz, string? servicioTelefonia,
        string? nombrePropietarioOrigen, string? nombreVia, string? direccionBarrio,
        string? direccionUrbana, string? usoTerreno, string? topografiaTerreno) => new()
    {
        DatasetVersionId = datasetVersionId, Geometria = geometria, CodUv = codUv, CodMan = codMan,
        CodPred = codPred, AtributosExtra = atributosExtra, FilaOrigen = filaOrigen,
        CodigoGeografico = codigoGeografico, Superficie = superficie, ValuacionZonal = valuacionZonal,
        TipoInmueble = tipoInmueble, ServicioAlcantarillado = servicioAlcantarillado,
        ServicioAgua = servicioAgua, ServicioLuz = servicioLuz, ServicioTelefonia = servicioTelefonia,
        NombrePropietarioOrigen = nombrePropietarioOrigen, NombreVia = nombreVia,
        DireccionBarrio = direccionBarrio, DireccionUrbana = direccionUrbana,
        UsoTerreno = usoTerreno, TopografiaTerreno = topografiaTerreno,
    };
}

public sealed class CapaEdificacion : CapaVersionada
{
    public MultiPolygon? Geometria { get; private set; }
    public long? IdEdificacionOrigen { get; private set; }
    public string? CodigoGeografico { get; private set; }
    public int? CodUv { get; private set; }
    public int? CodMan { get; private set; }
    public int? CodPred { get; private set; }
    public long? NumeroEdificacion { get; private set; }
    public long? Piso { get; private set; }
    public string? CodigoEspacio { get; private set; }
    public long? CodigoBloque { get; private set; }
    public decimal? AreaConstruida { get; private set; }

    private CapaEdificacion() { }

    public static CapaEdificacion Crear(Guid datasetVersionId, MultiPolygon? geometria, string atributosExtra,
        int filaOrigen, long? idEdificacionOrigen, string? codigoGeografico, int? codUv, int? codMan,
        int? codPred, long? numeroEdificacion, long? piso, string? codigoEspacio, long? codigoBloque,
        decimal? areaConstruida) => new()
    {
        DatasetVersionId = datasetVersionId, Geometria = geometria, AtributosExtra = atributosExtra,
        FilaOrigen = filaOrigen, IdEdificacionOrigen = idEdificacionOrigen, CodigoGeografico = codigoGeografico,
        CodUv = codUv, CodMan = codMan, CodPred = codPred, NumeroEdificacion = numeroEdificacion,
        Piso = piso, CodigoEspacio = codigoEspacio, CodigoBloque = codigoBloque, AreaConstruida = areaConstruida,
    };
}

public sealed class CapaPredioNoFotografiado : CapaVersionada
{
    public MultiPolygon? Geometria { get; private set; }
    public long? IdPredioOrigen { get; private set; }
    public string? CodigoGeografico { get; private set; }
    public int? CodUv { get; private set; }
    public int? CodMan { get; private set; }
    public int? CodPred { get; private set; }
    public string? IndicadorFotos { get; private set; }
    public string? FotoFrente { get; private set; }
    public string? FotoDerecha { get; private set; }
    public string? FotoIzquierda { get; private set; }

    private CapaPredioNoFotografiado() { }

    public static CapaPredioNoFotografiado Crear(Guid datasetVersionId, MultiPolygon? geometria, string atributosExtra,
        int filaOrigen, long? idPredioOrigen, string? codigoGeografico, int? codUv, int? codMan, int? codPred,
        string? indicadorFotos, string? fotoFrente, string? fotoDerecha, string? fotoIzquierda) => new()
    {
        DatasetVersionId = datasetVersionId, Geometria = geometria, AtributosExtra = atributosExtra,
        FilaOrigen = filaOrigen, IdPredioOrigen = idPredioOrigen, CodigoGeografico = codigoGeografico,
        CodUv = codUv, CodMan = codMan, CodPred = codPred, IndicadorFotos = indicadorFotos,
        FotoFrente = fotoFrente, FotoDerecha = fotoDerecha, FotoIzquierda = fotoIzquierda,
    };
}

public sealed class CapaManzana : CapaVersionada
{
    public MultiPolygon? Geometria { get; private set; }
    public string? CodigoGeografico { get; private set; }
    public int? CodUv { get; private set; }
    public int? CodMan { get; private set; }
    public decimal? CoordenadaOrigen { get; private set; }

    private CapaManzana() { }

    public static CapaManzana Crear(Guid datasetVersionId, MultiPolygon? geometria, string atributosExtra,
        int filaOrigen, string? codigoGeografico, int? codUv, int? codMan, decimal? coordenadaOrigen) => new()
    {
        DatasetVersionId = datasetVersionId, Geometria = geometria, AtributosExtra = atributosExtra,
        FilaOrigen = filaOrigen, CodigoGeografico = codigoGeografico, CodUv = codUv, CodMan = codMan,
        CoordenadaOrigen = coordenadaOrigen,
    };
}

public sealed class CapaDistrito : CapaVersionada
{
    public MultiPolygon? Geometria { get; private set; }
    public string? CodigoGeografico { get; private set; }
    public int? CodUv { get; private set; }
    public string? Nombre { get; private set; }

    private CapaDistrito() { }

    public static CapaDistrito Crear(Guid datasetVersionId, MultiPolygon? geometria, string atributosExtra,
        int filaOrigen, string? codigoGeografico, int? codUv, string? nombre) => new()
    {
        DatasetVersionId = datasetVersionId, Geometria = geometria, AtributosExtra = atributosExtra,
        FilaOrigen = filaOrigen, CodigoGeografico = codigoGeografico, CodUv = codUv, Nombre = nombre,
    };
}

public sealed class CapaZona : CapaVersionada
{
    public MultiPolygon? Geometria { get; private set; }
    public string? NombreZona { get; private set; }
    public long? IdZonaOrigen { get; private set; }
    public string? CodigoGeografico { get; private set; }

    private CapaZona() { }

    public static CapaZona Crear(Guid datasetVersionId, MultiPolygon? geometria, string atributosExtra,
        int filaOrigen, string? nombreZona, long? idZonaOrigen, string? codigoGeografico) => new()
    {
        DatasetVersionId = datasetVersionId, Geometria = geometria, AtributosExtra = atributosExtra,
        FilaOrigen = filaOrigen, NombreZona = nombreZona, IdZonaOrigen = idZonaOrigen,
        CodigoGeografico = codigoGeografico,
    };
}

public sealed class CapaVia : CapaVersionada
{
    public MultiLineString? Geometria { get; private set; }
    public string? Material { get; private set; }
    public string? Nombre { get; private set; }
    public string? Tipo { get; private set; }
    public decimal? DistanciaOrigen { get; private set; }

    private CapaVia() { }

    public static CapaVia Crear(Guid datasetVersionId, MultiLineString? geometria, string atributosExtra,
        int filaOrigen, string? material, string? nombre, string? tipo, decimal? distanciaOrigen) => new()
    {
        DatasetVersionId = datasetVersionId, Geometria = geometria, AtributosExtra = atributosExtra,
        FilaOrigen = filaOrigen, Material = material, Nombre = nombre, Tipo = tipo,
        DistanciaOrigen = distanciaOrigen,
    };
}

public sealed class CapaAreaUrbana : CapaVersionada
{
    public Geometry? Geometria { get; private set; }

    private CapaAreaUrbana() { }

    public static CapaAreaUrbana Crear(
        Guid datasetVersionId,
        Geometry? geometria,
        string atributosExtra,
        int filaOrigen) => new()
    {
        DatasetVersionId = datasetVersionId,
        Geometria = geometria,
        AtributosExtra = atributosExtra,
        FilaOrigen = filaOrigen,
    };
}

public sealed class CapaPuntoGeodesico : CapaVersionada
{
    public Point? Geometria { get; private set; }

    private CapaPuntoGeodesico() { }

    public static CapaPuntoGeodesico Crear(
        Guid datasetVersionId,
        Point? geometria,
        string atributosExtra,
        int filaOrigen) => new()
    {
        DatasetVersionId = datasetVersionId,
        Geometria = geometria,
        AtributosExtra = atributosExtra,
        FilaOrigen = filaOrigen,
    };
}
