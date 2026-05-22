using SG.Domain.Common;

namespace SG.Domain.Catastro.ValueObjects;

/// <summary>
/// Código catastral PROVISIONAL Caranavi — formato {DEP(2)}-{PROV(3)}-{MUN(3)}-{ZONA(3)}-{MZN(4)}-{LOTE(4)}.
/// Acepta entrada con o sin guiones. Normaliza a forma canónica con guiones. Ver ADR 0030.
/// </summary>
public sealed class CodigoCatastral : ValueObject
{
    private static readonly int[] LongitudesSegmentos = [2, 3, 3, 3, 4, 4];
    private const int LargoSinGuiones = 19; // 2+3+3+3+4+4

    public string Departamento { get; private set; } = string.Empty;
    public string Provincia { get; private set; } = string.Empty;
    public string Municipio { get; private set; } = string.Empty;
    public string Zona { get; private set; } = string.Empty;
    public string Manzana { get; private set; } = string.Empty;
    public string Lote { get; private set; } = string.Empty;

    public string Valor => $"{Departamento}-{Provincia}-{Municipio}-{Zona}-{Manzana}-{Lote}";

    private CodigoCatastral() { }

    private CodigoCatastral(string dep, string prov, string mun, string zona, string mzn, string lote)
    {
        Departamento = dep;
        Provincia = prov;
        Municipio = mun;
        Zona = zona;
        Manzana = mzn;
        Lote = lote;
    }

    public static Result<CodigoCatastral> Crear(string entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            return Result.Failure<CodigoCatastral>(CodigoCatastralErrores.EntradaVacia);

        string[] partes;
        var limpia = entrada.Trim();

        if (limpia.Contains('-'))
        {
            partes = limpia.Split('-');
        }
        else
        {
            var solo = limpia.Replace(" ", "");
            if (solo.Length != LargoSinGuiones)
                return Result.Failure<CodigoCatastral>(CodigoCatastralErrores.FormatoInvalido);

            partes = new string[LongitudesSegmentos.Length];
            int offset = 0;
            for (int i = 0; i < LongitudesSegmentos.Length; i++)
            {
                partes[i] = solo.Substring(offset, LongitudesSegmentos[i]);
                offset += LongitudesSegmentos[i];
            }
        }

        if (partes.Length != LongitudesSegmentos.Length)
            return Result.Failure<CodigoCatastral>(CodigoCatastralErrores.FormatoInvalido);

        for (int i = 0; i < LongitudesSegmentos.Length; i++)
        {
            var seg = partes[i].Trim();
            if (seg.Length != LongitudesSegmentos[i] || !TodosDigitos(seg))
                return Result.Failure<CodigoCatastral>(CodigoCatastralErrores.FormatoInvalido);
        }

        return Result.Success(new CodigoCatastral(
            partes[0].Trim(), partes[1].Trim(), partes[2].Trim(),
            partes[3].Trim(), partes[4].Trim(), partes[5].Trim()));
    }

    public static CodigoCatastral FromDb(string valor)
    {
        var result = Crear(valor);
        if (result.IsFailure)
            throw new InvalidOperationException($"Código catastral inválido en base de datos: '{valor}'");
        return result.Value;
    }

    private static bool TodosDigitos(string s)
    {
        foreach (var c in s)
            if (!char.IsDigit(c)) return false;
        return true;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Valor;
    }

    public override string ToString() => Valor;
}

public static class CodigoCatastralErrores
{
    public static readonly DomainError EntradaVacia = new("CodigoCatastral.EntradaVacia", "El código catastral no puede ser vacío.");
    public static readonly DomainError FormatoInvalido = new("CodigoCatastral.FormatoInvalido", "El código catastral no tiene el formato válido (ej: 02-004-001-001-0001-0001).");
}
