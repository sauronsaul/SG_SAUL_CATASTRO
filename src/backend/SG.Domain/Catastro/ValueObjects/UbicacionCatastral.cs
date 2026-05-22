using SG.Domain.Common;

namespace SG.Domain.Catastro.ValueObjects;

public sealed class UbicacionCatastral : ValueObject
{
    public string Zona { get; private set; } = string.Empty;
    public string Manzana { get; private set; } = string.Empty;
    public string Lote { get; private set; } = string.Empty;
    public string? Barrio { get; private set; }
    public string? Direccion { get; private set; }
    public string? Referencia { get; private set; }

    private UbicacionCatastral() { }

    private UbicacionCatastral(string zona, string manzana, string lote,
        string? barrio, string? direccion, string? referencia)
    {
        Zona = zona;
        Manzana = manzana;
        Lote = lote;
        Barrio = barrio;
        Direccion = direccion;
        Referencia = referencia;
    }

    public static Result<UbicacionCatastral> Crear(string zona, string manzana, string lote,
        string? barrio = null, string? direccion = null, string? referencia = null)
    {
        if (string.IsNullOrWhiteSpace(zona))
            return Result.Failure<UbicacionCatastral>(UbicacionCatastralErrores.ZonaRequerida);

        if (string.IsNullOrWhiteSpace(manzana))
            return Result.Failure<UbicacionCatastral>(UbicacionCatastralErrores.ManzanaRequerida);

        if (string.IsNullOrWhiteSpace(lote))
            return Result.Failure<UbicacionCatastral>(UbicacionCatastralErrores.LoteRequerido);

        return Result.Success(new UbicacionCatastral(
            zona.Trim(),
            manzana.Trim(),
            lote.Trim(),
            barrio?.Trim(),
            direccion?.Trim(),
            referencia?.Trim()));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Zona;
        yield return Manzana;
        yield return Lote;
    }

    public override string ToString() => $"Zona {Zona}, Mzn {Manzana}, Lote {Lote}";
}

public static class UbicacionCatastralErrores
{
    public static readonly DomainError ZonaRequerida = new("UbicacionCatastral.ZonaRequerida", "La zona de la ubicación catastral es requerida.");
    public static readonly DomainError ManzanaRequerida = new("UbicacionCatastral.ManzanaRequerida", "La manzana de la ubicación catastral es requerida.");
    public static readonly DomainError LoteRequerido = new("UbicacionCatastral.LoteRequerido", "El lote de la ubicación catastral es requerido.");
}
