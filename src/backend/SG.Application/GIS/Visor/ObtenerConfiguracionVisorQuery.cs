using MediatR;
using SG.Application.Abstractions.Catalogos;
using SG.Application.Abstractions.GIS;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.GIS;
using SG.Domain.Catalogos;
using SG.Domain.Common;
using SG.Domain.Importacion;

namespace SG.Application.GIS.Visor;

public sealed record ObtenerConfiguracionVisorQuery(string MunicipioCodigo)
    : IRequest<Result<ConfiguracionVisorDto>>;

internal sealed class ObtenerConfiguracionVisorQueryHandler(
    IMunicipioRepositorio municipios,
    IDatasetVersionRepositorio versiones,
    IEsquemaCapasMunicipioRepositorio esquemas,
    IExtensionMunicipalService extension)
    : IRequestHandler<ObtenerConfiguracionVisorQuery, Result<ConfiguracionVisorDto>>
{
    public async Task<Result<ConfiguracionVisorDto>> Handle(
        ObtenerConfiguracionVisorQuery request,
        CancellationToken cancellationToken)
    {
        if (!Municipio.EsCodigoIneValido(request.MunicipioCodigo))
            return Result.Failure<ConfiguracionVisorDto>(ErroresVisor.MunicipioCodigoInvalido);

        var municipio = await municipios.ObtenerPorCodigoIneAsync(request.MunicipioCodigo, cancellationToken);
        if (municipio is null)
            return Result.Failure<ConfiguracionVisorDto>(ErroresVisor.MunicipioNoEncontrado);

        var version = await versiones.ObtenerActivaAsync(request.MunicipioCodigo, cancellationToken);
        if (version is null)
            return Result.Failure<ConfiguracionVisorDto>(ErroresVisor.DatasetActivoNoDisponible);

        var esquema = await esquemas.ListarAsync(request.MunicipioCodigo, cancellationToken);
        if (esquema.Count == 0)
            return Result.Failure<ConfiguracionVisorDto>(ErroresVisor.EsquemaNoConfigurado);

        var bbox = await extension.ObtenerAsync(
            request.MunicipioCodigo,
            version.Id,
            cancellationToken);
        if (bbox is null)
            return Result.Failure<ConfiguracionVisorDto>(ErroresVisor.DatasetSinGeometrias);

        var tipos = esquema.Select(x => x.TipoCapa).ToHashSet();
        var capas = tipos
            .Select(CatalogoPresentacionCapasVisor.Obtener)
            .OrderBy(x => x.Orden)
            .ToArray();

        return Result.Success(new ConfiguracionVisorDto(
            new MunicipioVisorDto(municipio.CodigoIne, municipio.Nombre, municipio.NombreOficial),
            version.NumeroVersion,
            bbox,
            capas,
            new CapacidadesVisorDto(tipos.Contains(TipoCapa.Predios))));
    }
}

public static class ErroresVisor
{
    public static readonly DomainError MunicipioCodigoInvalido = new(
        "Visor.MunicipioCodigoInvalido",
        "El código INE del municipio debe contener exactamente seis dígitos.");
    public static readonly DomainError MunicipioNoEncontrado = new(
        "Visor.MunicipioNoEncontrado",
        "El municipio solicitado no existe.");
    public static readonly DomainError DatasetActivoNoDisponible = new(
        "Visor.DatasetActivoNoDisponible",
        "El municipio no tiene un dataset activo.");
    public static readonly DomainError EsquemaNoConfigurado = new(
        "Visor.EsquemaNoConfigurado",
        "El municipio no tiene un esquema de capas configurado.");
    public static readonly DomainError DatasetSinGeometrias = new(
        "Visor.DatasetSinGeometrias",
        "El dataset activo no contiene geometrías para calcular la extensión.");
}
