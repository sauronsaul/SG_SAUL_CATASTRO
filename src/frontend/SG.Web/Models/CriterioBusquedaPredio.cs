namespace SG.Web.Models;

public sealed record CriterioBusquedaPredio(int Distrito, int Manzana, int Predio)
{
    public static ResultadoCriterioBusqueda Crear(
        int? distrito,
        int? manzana,
        int? predio)
    {
        if (distrito is null || distrito < 1)
            return ResultadoCriterioBusqueda.Fallo("Ingrese un distrito mayor o igual a 1.");
        if (manzana is null || manzana < 1)
            return ResultadoCriterioBusqueda.Fallo("Ingrese una manzana mayor o igual a 1.");
        if (predio is null || predio < 1)
            return ResultadoCriterioBusqueda.Fallo("Ingrese un predio mayor o igual a 1.");

        return ResultadoCriterioBusqueda.Exito(new CriterioBusquedaPredio(
            distrito.Value,
            manzana.Value,
            predio.Value));
    }
}

public sealed record ResultadoCriterioBusqueda(
    CriterioBusquedaPredio? Criterio,
    string? Error)
{
    public bool EsValido => Criterio is not null;

    public static ResultadoCriterioBusqueda Exito(CriterioBusquedaPredio criterio) =>
        new(criterio, null);

    public static ResultadoCriterioBusqueda Fallo(string error) =>
        new(null, error);
}
