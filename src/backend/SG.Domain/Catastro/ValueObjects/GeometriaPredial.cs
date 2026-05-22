using NetTopologySuite.Geometries;
using SG.Domain.Common;

namespace SG.Domain.Catastro.ValueObjects;

public sealed class GeometriaPredial : ValueObject
{
    public const int SridObligatorio = 32719;

    public Polygon Poligono { get; private set; } = null!;

    private GeometriaPredial() { }

    private GeometriaPredial(Polygon poligono)
    {
        Poligono = poligono;
    }

    public static Result<GeometriaPredial> Crear(Polygon poligono)
    {
        if (poligono is null)
            return Result.Failure<GeometriaPredial>(GeometriaPredialErrores.PoligonoRequerido);

        if (poligono.SRID != SridObligatorio)
            return Result.Failure<GeometriaPredial>(GeometriaPredialErrores.SridInvalido);

        if (!poligono.IsValid)
            return Result.Failure<GeometriaPredial>(GeometriaPredialErrores.GeometriaInvalida);

        return Result.Success(new GeometriaPredial(poligono));
    }

    public double CalcularAreaM2() => Poligono.Area;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Poligono.AsText();
    }

    public override string ToString() => $"Polygon SRID={SridObligatorio}, Area={CalcularAreaM2():F2}m²";
}

public static class GeometriaPredialErrores
{
    public static readonly DomainError PoligonoRequerido = new("GeometriaPredial.PoligonoRequerido", "El polígono de la geometría predial es requerido.");
    public static readonly DomainError SridInvalido = new("GeometriaPredial.SridInvalido", $"La geometría debe estar en SRID {GeometriaPredial.SridObligatorio} (UTM WGS84 Zona 19S).");
    public static readonly DomainError GeometriaInvalida = new("GeometriaPredial.GeometriaInvalida", "La geometría del polígono no es válida topológicamente.");
}
