using System.Globalization;
using System.Text;
using SG.Contracts.Catastro;

namespace SG.Web.Models;

public static class CroquisSvg
{
    public const double Ancho = 900;
    public const double Alto = 560;
    private const double MargenHorizontal = 55;
    private const double MargenSuperior = 45;
    private const double MargenInferior = 105;
    private const double AnchoImpresionMilimetros = 180;

    public static CroquisSvgResultado Crear(GeometriaPlanarDto geometria)
    {
        Validar(geometria);
        var posiciones = geometria.Coordenadas.SelectMany(anillo => anillo).ToArray();
        var minimoX = posiciones.Min(posicion => posicion[0]);
        var maximoX = posiciones.Max(posicion => posicion[0]);
        var minimoY = posiciones.Min(posicion => posicion[1]);
        var maximoY = posiciones.Max(posicion => posicion[1]);
        var anchoMetros = maximoX - minimoX;
        var altoMetros = maximoY - minimoY;
        if (anchoMetros <= 0 || altoMetros <= 0)
            throw new ArgumentException("La geometria planar debe ocupar un area positiva.", nameof(geometria));

        var anchoDisponible = Ancho - (2 * MargenHorizontal);
        var altoDisponible = Alto - MargenSuperior - MargenInferior;
        var unidadesSvgPorMetro = Math.Min(
            anchoDisponible / anchoMetros,
            altoDisponible / altoMetros);
        var desplazamientoX = MargenHorizontal
            + ((anchoDisponible - (anchoMetros * unidadesSvgPorMetro)) / 2);
        var desplazamientoY = MargenSuperior
            + ((altoDisponible - (altoMetros * unidadesSvgPorMetro)) / 2);

        var trayectoria = new StringBuilder();
        foreach (var anillo in geometria.Coordenadas)
        {
            for (var indice = 0; indice < anillo.Length; indice++)
            {
                var x = desplazamientoX + ((anillo[indice][0] - minimoX) * unidadesSvgPorMetro);
                var y = desplazamientoY + ((maximoY - anillo[indice][1]) * unidadesSvgPorMetro);
                trayectoria.Append(indice == 0 ? "M " : " L ");
                trayectoria.Append(Formatear(x));
                trayectoria.Append(' ');
                trayectoria.Append(Formatear(y));
            }

            trayectoria.Append(" Z ");
        }

        var objetivoBarraMetros = (anchoDisponible * 0.25) / unidadesSvgPorMetro;
        var barraMetros = ObtenerDistanciaLegible(objetivoBarraMetros);
        var barraUnidadesSvg = barraMetros * unidadesSvgPorMetro;
        var barraX1 = MargenHorizontal;
        var barraX2 = barraX1 + barraUnidadesSvg;
        var barraY = Alto - 50;
        var metrosPorUnidadSvg = 1 / unidadesSvgPorMetro;
        var milimetrosPorUnidadSvg = AnchoImpresionMilimetros / Ancho;
        var escalaNominal = (int)Math.Max(
            1,
            Math.Round((metrosPorUnidadSvg * 1000) / milimetrosPorUnidadSvg / 10) * 10);

        return new CroquisSvgResultado(
            trayectoria.ToString().Trim(),
            barraX1,
            barraX2,
            barraY,
            barraMetros,
            metrosPorUnidadSvg,
            escalaNominal);
    }

    private static void Validar(GeometriaPlanarDto geometria)
    {
        ArgumentNullException.ThrowIfNull(geometria);
        if (geometria.Srid != 32719)
            throw new ArgumentException("El croquis requiere coordenadas EPSG:32719.", nameof(geometria));
        if (!string.Equals(geometria.Tipo, "Polygon", StringComparison.Ordinal))
            throw new ArgumentException("El croquis requiere una geometria Polygon.", nameof(geometria));
        if (geometria.Coordenadas.Length == 0)
            throw new ArgumentException("La geometria no contiene anillos.", nameof(geometria));

        foreach (var anillo in geometria.Coordenadas)
        {
            if (anillo.Length < 4)
                throw new ArgumentException("Cada anillo debe contener al menos cuatro posiciones.", nameof(geometria));
            if (anillo.Any(posicion => posicion.Length != 2
                    || !double.IsFinite(posicion[0])
                    || !double.IsFinite(posicion[1])))
            {
                throw new ArgumentException("La geometria contiene una posicion planar invalida.", nameof(geometria));
            }
        }
    }

    private static double ObtenerDistanciaLegible(double objetivo)
    {
        var potencia = Math.Pow(10, Math.Floor(Math.Log10(objetivo)));
        var normalizada = objetivo / potencia;
        var baseLegible = normalizada >= 5 ? 5 : normalizada >= 2 ? 2 : 1;
        return baseLegible * potencia;
    }

    private static string Formatear(double valor) =>
        valor.ToString("0.###", CultureInfo.InvariantCulture);
}

public sealed record CroquisSvgResultado(
    string Trayectoria,
    double BarraX1,
    double BarraX2,
    double BarraY,
    double BarraMetros,
    double MetrosPorUnidadSvg,
    int EscalaNominal);

public static class FechaEmisionBolivia
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-4);

    public static DateTimeOffset DesdeUtc(DateTimeOffset fechaUtc) =>
        fechaUtc.ToUniversalTime().ToOffset(Offset);
}
