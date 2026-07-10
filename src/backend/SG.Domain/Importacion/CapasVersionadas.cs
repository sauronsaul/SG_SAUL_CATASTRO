using NetTopologySuite.Geometries;
using SG.Domain.Common;

namespace SG.Domain.Importacion;

public abstract class CapaVersionada : Entity
{
    public Guid DatasetVersionId { get; private set; }
    public string AtributosExtra { get; private set; } = "{}";
    public int FilaOrigen { get; private set; }

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
}

public sealed class CapaEdificacion : CapaVersionada
{
    public Polygon Geometria { get; private set; } = null!;
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
}

public sealed class CapaPredioNoFotografiado : CapaVersionada
{
    public Polygon Geometria { get; private set; } = null!;
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
}

public sealed class CapaManzana : CapaVersionada
{
    public Polygon Geometria { get; private set; } = null!;
    public string? CodigoGeografico { get; private set; }
    public int? CodUv { get; private set; }
    public int? CodMan { get; private set; }
    public decimal? CoordenadaOrigen { get; private set; }

    private CapaManzana() { }
}

public sealed class CapaDistrito : CapaVersionada
{
    public Polygon Geometria { get; private set; } = null!;
    public string? CodigoGeografico { get; private set; }
    public int? CodUv { get; private set; }
    public string? Nombre { get; private set; }

    private CapaDistrito() { }
}

public sealed class CapaZona : CapaVersionada
{
    public Polygon Geometria { get; private set; } = null!;
    public string? NombreZona { get; private set; }
    public long? IdZonaOrigen { get; private set; }
    public string? CodigoGeografico { get; private set; }

    private CapaZona() { }
}

public sealed class CapaVia : CapaVersionada
{
    public LineString Geometria { get; private set; } = null!;
    public string? Material { get; private set; }
    public string? Nombre { get; private set; }
    public string? Tipo { get; private set; }
    public decimal? DistanciaOrigen { get; private set; }

    private CapaVia() { }
}
