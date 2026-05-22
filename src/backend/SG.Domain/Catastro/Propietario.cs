using SG.Domain.Catastro.Enums;
using SG.Domain.Common;

namespace SG.Domain.Catastro;

public sealed class Propietario : AggregateRoot
{
    public TipoPropietario Tipo { get; private set; }

    // PersonaNatural
    public string? Nombre { get; private set; }
    public string? Apellidos { get; private set; }
    public string? Cedula { get; private set; }

    // PersonaJuridica
    public string? RazonSocial { get; private set; }
    public string? Nit { get; private set; }
    public string? RepresentanteLegal { get; private set; }

    // Comunes
    public string? Email { get; private set; }
    public string? Telefono { get; private set; }
    public string? Direccion { get; private set; }

    public string NombreCompleto => Tipo == TipoPropietario.PersonaNatural
        ? $"{Nombre} {Apellidos}".Trim()
        : RazonSocial ?? string.Empty;

    private Propietario() { }

    public static Result<Propietario> CrearPersonaNatural(
        string nombre, string apellidos, string cedula,
        string? email = null, string? telefono = null, string? direccion = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            return Result.Failure<Propietario>(PropietarioErrores.NombreRequerido);

        if (string.IsNullOrWhiteSpace(apellidos))
            return Result.Failure<Propietario>(PropietarioErrores.ApellidosRequeridos);

        if (string.IsNullOrWhiteSpace(cedula))
            return Result.Failure<Propietario>(PropietarioErrores.CedulaRequerida);

        if (cedula.Trim().Length > 15)
            return Result.Failure<Propietario>(PropietarioErrores.CedulaInvalida);

        return Result.Success(new Propietario
        {
            Tipo = TipoPropietario.PersonaNatural,
            Nombre = nombre.Trim(),
            Apellidos = apellidos.Trim(),
            Cedula = cedula.Trim(),
            Email = email?.Trim(),
            Telefono = telefono?.Trim(),
            Direccion = direccion?.Trim(),
        });
    }

    public static Result<Propietario> CrearPersonaJuridica(
        string razonSocial, string nit,
        string? representanteLegal = null,
        string? email = null, string? telefono = null, string? direccion = null)
    {
        if (string.IsNullOrWhiteSpace(razonSocial))
            return Result.Failure<Propietario>(PropietarioErrores.RazonSocialRequerida);

        if (string.IsNullOrWhiteSpace(nit))
            return Result.Failure<Propietario>(PropietarioErrores.NitRequerido);

        if (!TodosDigitos(nit.Trim()) || nit.Trim().Length > 13)
            return Result.Failure<Propietario>(PropietarioErrores.NitInvalido);

        return Result.Success(new Propietario
        {
            Tipo = TipoPropietario.PersonaJuridica,
            RazonSocial = razonSocial.Trim(),
            Nit = nit.Trim(),
            RepresentanteLegal = representanteLegal?.Trim(),
            Email = email?.Trim(),
            Telefono = telefono?.Trim(),
            Direccion = direccion?.Trim(),
        });
    }

    public Result ActualizarContacto(string? email, string? telefono, string? direccion)
    {
        Email = email?.Trim();
        Telefono = telefono?.Trim();
        Direccion = direccion?.Trim();
        return Result.Success();
    }

    private static bool TodosDigitos(string s)
    {
        foreach (var c in s)
            if (!char.IsDigit(c)) return false;
        return true;
    }
}

public static class PropietarioErrores
{
    public static readonly DomainError NombreRequerido = new("Propietario.NombreRequerido", "El nombre del propietario es requerido.");
    public static readonly DomainError ApellidosRequeridos = new("Propietario.ApellidosRequeridos", "Los apellidos del propietario son requeridos.");
    public static readonly DomainError CedulaRequerida = new("Propietario.CedulaRequerida", "La cédula de identidad es requerida para persona natural.");
    public static readonly DomainError CedulaInvalida = new("Propietario.CedulaInvalida", "La cédula de identidad tiene un formato inválido.");
    public static readonly DomainError RazonSocialRequerida = new("Propietario.RazonSocialRequerida", "La razón social es requerida para persona jurídica.");
    public static readonly DomainError NitRequerido = new("Propietario.NitRequerido", "El NIT es requerido para persona jurídica.");
    public static readonly DomainError NitInvalido = new("Propietario.NitInvalido", "El NIT debe ser numérico y tener hasta 13 dígitos.");
    public static readonly DomainError CedulaDuplicada = new("Propietario.CedulaDuplicada", "Ya existe un propietario registrado con esa cédula de identidad.");
    public static readonly DomainError NitDuplicado = new("Propietario.NitDuplicado", "Ya existe un propietario registrado con ese NIT.");
    public static readonly DomainError NoEncontrado = new("Propietario.NoEncontrado", "El propietario no fue encontrado.");
}
