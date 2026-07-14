namespace SG.Web.Models;

public sealed record ConfiguracionVisor(string MunicipioCodigo, IReadOnlyList<double> Limites);
