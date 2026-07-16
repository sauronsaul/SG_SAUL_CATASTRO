using System.IO.Compression;
using MediatR;
using Microsoft.Extensions.Options;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Importacion;
using SG.Application.Catastro.Config;
using SG.Contracts.Importacion;
using SG.Domain.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public sealed class CrearVersionImportacionHandler(
    IPerfilImportacionRepositorio perfiles,
    IDatasetVersionRepositorio versiones,
    IMinioService minio,
    IColaCargaVersionada cola,
    IEsquemaCapasMunicipioRepositorio esquemas,
    IOptions<CatastroConfig> config)
    : IRequestHandler<CrearVersionImportacionCommand, Result<CrearVersionImportacionDto>>
{
    private const long TamanoMaximoBytes = 110L * 1024 * 1024;

    public async Task<Result<CrearVersionImportacionDto>> Handle(
        CrearVersionImportacionCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TamanoBytes is <= 0 or > TamanoMaximoBytes ||
            string.IsNullOrWhiteSpace(request.NombreArchivo) ||
            !Path.GetExtension(request.NombreArchivo).Equals(".zip", StringComparison.OrdinalIgnoreCase))
            return Result.Failure<CrearVersionImportacionDto>(VersionImportacionErrores.PaqueteInvalido);

        using var paquete = new MemoryStream();
        await request.PaqueteStream.CopyToAsync(paquete, cancellationToken);

        // TODO 3.A.2b: el municipio objetivo debe llegar en el contrato de importacion.
        var municipioCodigo = config.Value.MunicipioCodigo;
        var esquemaMunicipal = await esquemas.ListarAsync(municipioCodigo, cancellationToken);
        if (esquemaMunicipal.Count == 0)
            return Result.Failure<CrearVersionImportacionDto>(VersionImportacionErrores.PaqueteInvalido);
        var perfilesVersionados = await ObtenerPerfilesVersionadosAsync(esquemaMunicipal, cancellationToken);
        if (perfilesVersionados is null || !ContieneArchivosEsperados(paquete, perfilesVersionados))
            return Result.Failure<CrearVersionImportacionDto>(VersionImportacionErrores.PaqueteInvalido);

        paquete.Position = 0;
        var claveMinio = $"importaciones/versiones/{Guid.NewGuid():N}/{SanitizarNombre(request.NombreArchivo)}.zip";
        await minio.SubirAsync(
            paquete,
            claveMinio,
            "application/zip",
            paquete.Length,
            cancellationToken);

        var numeroVersion = await versiones.ObtenerSiguienteNumeroAsync(
            municipioCodigo,
            cancellationToken);
        var version = DatasetVersion.Crear(
            numeroVersion,
            municipioCodigo,
            importacionId: null,
            origenDescripcion: $"Paquete de {esquemaMunicipal.Count} capas: {request.NombreArchivo}",
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

        foreach (var definicion in esquemaMunicipal.Where(x => x.Obligatoria))
        {
            var perfil = disponibles.FirstOrDefault(x => x.Nombre == definicion.NombrePerfil);
            if (perfil is null || perfil.TipoCapa != definicion.TipoCapa)
                return null;

            resultado.Add(perfil);
        }

        return resultado;
    }

    private static bool ContieneArchivosEsperados(
        MemoryStream paquete,
        IReadOnlyList<PerfilImportacion> perfilesVersionados)
    {
        try
        {
            paquete.Position = 0;
            using var zip = new ZipArchive(paquete, ZipArchiveMode.Read, leaveOpen: true);
            var archivos = zip.Entries
                .Where(x => !string.IsNullOrEmpty(x.Name))
                // ZipExtractor busca los SHP en la raíz. Rechazar rutas anidadas aquí evita
                // que un request malformado termine como carga Fallida.
                .Select(x => x.FullName.Replace('\\', '/'))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // TODO 3.A.2b: distinguir componentes obligatorios de capas opcionales.
            return perfilesVersionados.All(perfil =>
            {
                var baseNombre = Path.GetFileNameWithoutExtension(perfil.NombreArchivoShp);
                return archivos.Contains(baseNombre + ".shp") &&
                       archivos.Contains(baseNombre + ".dbf") &&
                       archivos.Contains(baseNombre + ".shx") &&
                       archivos.Contains(baseNombre + ".prj");
            });
        }
        catch (InvalidDataException)
        {
            return false;
        }
        finally
        {
            paquete.Position = 0;
        }
    }

    private static string SanitizarNombre(string nombre)
    {
        var sinExtension = Path.GetFileNameWithoutExtension(nombre);
        var invalidos = Path.GetInvalidFileNameChars();
        return new string(sinExtension.Select(c => invalidos.Contains(c) ? '_' : c).ToArray());
    }
}
