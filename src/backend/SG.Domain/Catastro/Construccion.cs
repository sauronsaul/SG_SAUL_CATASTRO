using SG.Domain.Common;

namespace SG.Domain.Catastro;

public sealed class Construccion : Entity
{
    public Guid PredioId { get; private set; }
    public int Numero { get; private set; }
    public int Pisos { get; private set; }
    public string? Bloque { get; private set; }
    public decimal AreaConstruida { get; private set; }
    public string? TipoConstruccion { get; private set; }

    private Construccion() { }

    internal static Result<Construccion> Crear(
        Guid predioId,
        int numero,
        int pisos,
        string? bloque,
        decimal areaConstruida,
        string? tipoConstruccion)
    {
        if (areaConstruida <= 0)
            return Result.Failure<Construccion>(ConstruccionErrores.AreaInvalida);

        if (pisos < 0)
            return Result.Failure<Construccion>(ConstruccionErrores.PisosInvalidos);

        return Result.Success(new Construccion
        {
            PredioId = predioId,
            Numero = numero,
            Pisos = pisos,
            Bloque = bloque?.Trim(),
            AreaConstruida = areaConstruida,
            TipoConstruccion = tipoConstruccion?.Trim(),
        });
    }
}

public static class ConstruccionErrores
{
    public static readonly DomainError AreaInvalida =
        new("Construccion.AreaInvalida", "El área construida debe ser mayor a cero.");
    public static readonly DomainError PisosInvalidos =
        new("Construccion.PisosInvalidos", "El número de pisos no puede ser negativo.");
}
