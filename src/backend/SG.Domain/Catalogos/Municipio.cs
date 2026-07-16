using SG.Domain.Common;

namespace SG.Domain.Catalogos;

public sealed class Municipio : AggregateRoot
{
    public string CodigoIne { get; private set; } = string.Empty;
    public string Nombre { get; private set; } = string.Empty;
    public string NombreOficial { get; private set; } = string.Empty;
    public string Departamento { get; private set; } = string.Empty;
    public string FuenteCodigo { get; private set; } = string.Empty;

    private Municipio() { }

    public static Result<Municipio> Crear(
        string codigoIne,
        string nombre,
        string nombreOficial,
        string departamento,
        string fuenteCodigo)
    {
        if (!EsCodigoIneValido(codigoIne))
            return Result.Failure<Municipio>(MunicipioErrores.CodigoIneInvalido);
        if (string.IsNullOrWhiteSpace(nombre) ||
            string.IsNullOrWhiteSpace(nombreOficial) ||
            string.IsNullOrWhiteSpace(departamento) ||
            string.IsNullOrWhiteSpace(fuenteCodigo))
            return Result.Failure<Municipio>(MunicipioErrores.DatosRequeridos);

        return Result.Success(new Municipio
        {
            CodigoIne = codigoIne,
            Nombre = nombre.Trim(),
            NombreOficial = nombreOficial.Trim(),
            Departamento = departamento.Trim(),
            FuenteCodigo = fuenteCodigo.Trim(),
        });
    }

    public static bool EsCodigoIneValido(string? codigo) =>
        codigo is { Length: 6 } && codigo.All(c => c is >= '0' and <= '9');
}

public static class MunicipioErrores
{
    public static readonly DomainError CodigoIneInvalido = new(
        "Municipio.CodigoIneInvalido",
        "El codigo INE debe contener exactamente seis digitos ASCII.");

    public static readonly DomainError DatosRequeridos = new(
        "Municipio.DatosRequeridos",
        "El nombre, nombre oficial, departamento y fuente del codigo son requeridos.");
}
