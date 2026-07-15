namespace SG.Web.Models;

public sealed record ConfiguracionVisor(string MunicipioCodigo, IReadOnlyList<double> Limites)
{
    public double[] ObtenerLimitesMapa()
    {
        if (Limites.Count != 4
            || Limites.Any(limite => !double.IsFinite(limite))
            || Limites[0] >= Limites[2]
            || Limites[1] >= Limites[3])
        {
            throw new InvalidOperationException(
                "Visor:Mapa:Limites debe contener un bbox valido [oeste, sur, este, norte].");
        }

        return [.. Limites];
    }
}
