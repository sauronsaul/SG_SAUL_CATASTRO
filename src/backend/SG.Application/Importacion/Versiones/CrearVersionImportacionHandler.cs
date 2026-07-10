using System.IO.Compression;
using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Importacion;
using SG.Domain.Importacion;
using SG.Domain.Common;

namespace SG.Application.Importacion.Versiones;

public sealed class CrearVersionImportacionHandler(
    IPerfilImportacionRepositorio perfiles,
    IDatasetVersionRepositorio versiones,
    IMinioService minio,
    IColaCargaVersionada cola)
    : IRequestHandler<CrearVersionImportacionCommand, Result<CrearVersionImportacionDto>>
{
    private const long TamanoMaximoBytes = 110L * 1024 * 1024;

    public async Task<Result<CrearVersionImportacionDto>> Handle(
        CrearVersionImportacionCommand request,
        CancellationToken cancellationToken)
    {
        if (request.TamanoBytes is <= 0 or > TamanoMaximoBytes ||
            string.IsNullOrWhiteSpace(request.NombreArchivo))
            return Result.Failure<CrearVersionImportacionDto>(VersionImportacionErrores.PaqueteInvalido);

        using var paquete = new MemoryStream();
        await request.PaqueteStream.CopyToAsync(paquete, cancellationToken);

        var perfilesDisponibles = await perfiles.ListarAsync(cancellationToken);
        var perfilesVersionados = DefinicionesCapasVersionadasUyuni.Todas
            .Select(definicion => perfilesDisponibles.FirstOrDefault(x => x.Nombre == definicion.NombrePerfil))
            .ToList();

        if (perfilesVersionados.Any(x => x is null) || !ContieneArchivosEsperados(paquete))
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
            DefinicionesCapasVersionadasUyuni.MunicipioCodigo,
            cancellationToken);
        var version = DatasetVersion.Crear(
            numeroVersion,
            DefinicionesCapasVersionadasUyuni.MunicipioCodigo,
            importacionId: null,
            origenDescripcion: $"Paquete de siete capas: {request.NombreArchivo}",
            rutaMinioPaquete: claveMinio);

        versiones.Agregar(version);
        await versiones.GuardarCambiosAsync(cancellationToken);
        await cola.EncolarAsync(version.Id, cancellationToken);

        return Result.Success(new CrearVersionImportacionDto(version.Id, version.Estado.ToString()));
    }

    private static bool ContieneArchivosEsperados(MemoryStream paquete)
    {
        try
        {
            paquete.Position = 0;
            using var zip = new ZipArchive(paquete, ZipArchiveMode.Read, leaveOpen: true);
            var archivos = zip.Entries
                .Where(x => !string.IsNullOrEmpty(x.Name))
                .Select(x => x.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return DefinicionesCapasVersionadasUyuni.Todas.All(definicion =>
            {
                var baseNombre = Path.GetFileNameWithoutExtension(definicion.NombreArchivoShp);
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
