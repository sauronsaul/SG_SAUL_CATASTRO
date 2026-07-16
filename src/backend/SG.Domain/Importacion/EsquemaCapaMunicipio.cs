using SG.Domain.Catalogos;
using SG.Domain.Common;

namespace SG.Domain.Importacion;

public sealed class EsquemaCapaMunicipio : AggregateRoot
{
    public string MunicipioCodigo { get; private set; } = string.Empty;
    public TipoCapa TipoCapa { get; private set; }
    public string NombrePerfil { get; private set; } = string.Empty;
    public string NombreArchivoShp { get; private set; } = string.Empty;
    public string TablaDestino { get; private set; } = string.Empty;
    public bool Obligatoria { get; private set; }

    private EsquemaCapaMunicipio() { }

    public static Result<EsquemaCapaMunicipio> Crear(
        string municipioCodigo,
        TipoCapa tipoCapa,
        string nombrePerfil,
        string nombreArchivoShp,
        string tablaDestino,
        bool obligatoria)
    {
        if (!Municipio.EsCodigoIneValido(municipioCodigo))
            return Result.Failure<EsquemaCapaMunicipio>(MunicipioErrores.CodigoIneInvalido);
        if (string.IsNullOrWhiteSpace(nombrePerfil) ||
            string.IsNullOrWhiteSpace(nombreArchivoShp) ||
            string.IsNullOrWhiteSpace(tablaDestino))
            return Result.Failure<EsquemaCapaMunicipio>(EsquemaCapaMunicipioErrores.DatosRequeridos);

        return Result.Success(new EsquemaCapaMunicipio
        {
            MunicipioCodigo = municipioCodigo,
            TipoCapa = tipoCapa,
            NombrePerfil = nombrePerfil.Trim(),
            NombreArchivoShp = nombreArchivoShp.Trim(),
            TablaDestino = tablaDestino.Trim(),
            Obligatoria = obligatoria,
        });
    }
}

public static class EsquemaCapaMunicipioErrores
{
    public static readonly DomainError DatosRequeridos = new(
        "EsquemaCapaMunicipio.DatosRequeridos",
        "El perfil, archivo SHP y tabla destino son requeridos.");
}
