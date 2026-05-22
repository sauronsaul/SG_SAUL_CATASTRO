using SG.Domain.Common;

namespace SG.Domain.Catalogos;

public sealed class UsoSuelo : AggregateRoot
{
    public string Codigo { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string? Descripcion { get; private set; }
    public bool Activo { get; private set; }
    public int Orden { get; private set; }

    private UsoSuelo() { }

    public static Result<UsoSuelo> Crear(string codigo, string nombre, int orden, string? descripcion = null)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            return Result.Failure<UsoSuelo>(UsoSueloErrores.CodigoRequerido);

        if (string.IsNullOrWhiteSpace(nombre))
            return Result.Failure<UsoSuelo>(UsoSueloErrores.NombreRequerido);

        if (orden < 0)
            return Result.Failure<UsoSuelo>(UsoSueloErrores.OrdenInvalido);

        var usoSuelo = new UsoSuelo
        {
            Codigo = codigo.Trim().ToUpperInvariant(),
            Nombre = nombre.Trim(),
            Descripcion = descripcion?.Trim(),
            Activo = true,
            Orden = orden,
        };

        return Result.Success(usoSuelo);
    }

    public Result Desactivar()
    {
        Activo = false;
        return Result.Success();
    }
}

public static class UsoSueloErrores
{
    public static readonly DomainError CodigoRequerido = new("UsoSuelo.CodigoRequerido", "El código de uso de suelo es requerido.");
    public static readonly DomainError NombreRequerido = new("UsoSuelo.NombreRequerido", "El nombre de uso de suelo es requerido.");
    public static readonly DomainError OrdenInvalido = new("UsoSuelo.OrdenInvalido", "El orden debe ser un número no negativo.");
    public static readonly DomainError NoEncontrado = new("UsoSuelo.NoEncontrado", "El uso de suelo no fue encontrado.");
}
