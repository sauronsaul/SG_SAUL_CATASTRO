using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Catalogos;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Importacion;
using SG.Domain.Catalogos;
using SG.Domain.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public sealed class CrearVersionImportacionHandler(
    IPerfilImportacionRepositorio perfiles,
    IDatasetVersionRepositorio versiones,
    IMinioService minio,
    IColaCargaVersionada cola,
    IEsquemaCapasMunicipioRepositorio esquemas,
    IMunicipioRepositorio municipios,
    IInspectorPaqueteVersionado inspectorPaquete)
    : IRequestHandler<CrearVersionImportacionCommand, Result<CrearVersionImportacionDto>>
{
    private const long TamanoMaximoBytes = 110L * 1024 * 1024;

    public async Task<Result<CrearVersionImportacionDto>> Handle(
        CrearVersionImportacionCommand request,
        CancellationToken cancellationToken)
    {
        if (!Municipio.EsCodigoIneValido(request.MunicipioCodigo))
            return Result.Failure<CrearVersionImportacionDto>(MunicipioErrores.CodigoIneInvalido);

        if (request.TamanoBytes is <= 0 or > TamanoMaximoBytes ||
            string.IsNullOrWhiteSpace(request.NombreArchivo) ||
            !Path.GetExtension(request.NombreArchivo).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<CrearVersionImportacionDto>(
                VersionImportacionErrores.PaqueteInvalido("nombre, extensión o tamaño no permitido"));

        if (!await municipios.ExistePorCodigoIneAsync(request.MunicipioCodigo, cancellationToken))
            return Result.Failure<CrearVersionImportacionDto>(
                VersionImportacionErrores.MunicipioNoEncontrado(request.MunicipioCodigo));

        var esquemaMunicipal = await esquemas.ListarAsync(request.MunicipioCodigo, cancellationToken);
        if (esquemaMunicipal.Count == 0)
            return Result.Failure<CrearVersionImportacionDto>(
                VersionImportacionErrores.EsquemaMunicipalNoConfigurado(request.MunicipioCodigo));

        var perfilesVersionados = await ObtenerPerfilesVersionadosAsync(esquemaMunicipal, cancellationToken);
        if (perfilesVersionados is null)
            return Result.Failure<CrearVersionImportacionDto>(
                VersionImportacionErrores.EsquemaMunicipalInconsistente(
                    "falta un perfil o su tipo de capa no coincide"));

        using var paquete = new MemoryStream();
        await request.PaqueteStream.CopyToAsync(paquete, cancellationToken);
        var inspeccion = inspectorPaquete.Inspeccionar(paquete, esquemaMunicipal);
        if (!inspeccion.EsValido)
            return Result.Failure<CrearVersionImportacionDto>(
                VersionImportacionErrores.PaqueteInvalido(string.Join(" ", inspeccion.Errores)));

        paquete.Position = 0;
        var claveMinio = $"importaciones/versiones/{Guid.NewGuid():N}/{SanitizarNombre(request.NombreArchivo)}.zip";
        await minio.SubirAsync(
            paquete,
            claveMinio,
            "application/zip",
            paquete.Length,
            cancellationToken);

        var numeroVersion = await versiones.ObtenerSiguienteNumeroAsync(
            request.MunicipioCodigo,
            cancellationToken);
        var descripcionCapas = esquemaMunicipal.Count == 1
            ? "1 capa"
            : $"{esquemaMunicipal.Count} capas";
        var version = DatasetVersion.Crear(
            numeroVersion,
            request.MunicipioCodigo,
            importacionId: null,
            origenDescripcion: $"Paquete de {descripcionCapas}: {request.NombreArchivo}",
            rutaMinioPaquete: claveMinio);

        versiones.Agregar(version);
        await versiones.GuardarCambiosAsync(cancellationToken);
        await cola.EncolarAsync(version.Id, cancellationToken);

        return Result.Success(new CrearVersionImportacionDto(version.Id, version.Estado.ToString()));
    }

    private async Task<IReadOnlyList<PerfilImportacion>?> ObtenerPerfilesVersionadosAsync(
        IReadOnlyList<EsquemaCapaMunicipio> esquemaMunicipal,
        CancellationToken ct)
    {
        var disponibles = await perfiles.ListarAsync(ct);
        var resultado = new List<PerfilImportacion>();

        foreach (var definicion in esquemaMunicipal)
        {
            var perfil = disponibles.FirstOrDefault(x => x.Nombre == definicion.NombrePerfil);
            if (perfil is null || perfil.TipoCapa != definicion.TipoCapa)
                return null;

            resultado.Add(perfil);
        }

        return resultado;
    }

    private static string SanitizarNombre(string nombre)
    {
        var sinExtension = Path.GetFileNameWithoutExtension(nombre);
        var invalidos = Path.GetInvalidFileNameChars();
        return new string(sinExtension.Select(c => invalidos.Contains(c) ? '_' : c).ToArray());
    }
}
