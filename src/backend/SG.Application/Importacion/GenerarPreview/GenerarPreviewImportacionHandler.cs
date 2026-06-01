using MediatR;
using SG.Application.Abstractions;
using SG.Application.Abstractions.Importacion;
using SG.Contracts.Importacion;
using SG.Domain.Common;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Application.Importacion.GenerarPreview;

public sealed class GenerarPreviewImportacionHandler(
    IPerfilImportacionRepositorio perfiles,
    IImportacionRepositorio importaciones,
    IMinioService minio,
    ICurrentUserService currentUser,
    PipelineShapefileService pipeline)
    : IRequestHandler<GenerarPreviewImportacionCommand, Result<PreviewImportacionDto>>
{
    private const int MaxMuestraPorCategoria = 20;

    public async Task<Result<PreviewImportacionDto>> Handle(
        GenerarPreviewImportacionCommand request,
        CancellationToken cancellationToken)
    {
        var perfil = await perfiles.ObtenerPorIdAsync(request.PerfilId, cancellationToken);
        if (perfil is null)
            return Result.Failure<PreviewImportacionDto>(ImportacionDomain.PerfilImportacionErrores.NoEncontrado);

        // Buffer en memoria: se reutiliza para el pipeline y luego para la subida a MinIO.
        using var zipBuffer = new MemoryStream();
        await request.ZipStream.CopyToAsync(zipBuffer, cancellationToken);
        zipBuffer.Position = 0;

        var filas = await pipeline.ProcesarAsync(zipBuffer, perfil, cancellationToken);

        var minioKey = $"importaciones/{Guid.NewGuid():N}/{SanitizarNombre(request.NombreArchivo)}.zip";
        zipBuffer.Position = 0;
        await minio.SubirAsync(
            zipBuffer,
            minioKey,
            "application/zip",
            zipBuffer.Length,
            cancellationToken);

        var usuarioId = currentUser.UserId ?? Guid.Empty;
        var importacion = ImportacionDomain.Importacion.CrearPreview(
            request.PerfilId,
            request.NombreArchivo,
            minioKey,
            usuarioId,
            filas.Count);

        importacion.RegistrarConteosPreview(
            filasACrear:         filas.Count(f => f.Accion == AccionPreviewFila.Crear),
            filasAActualizar:    filas.Count(f => f.Accion == AccionPreviewFila.Actualizar),
            filasAOmitir:        filas.Count(f => f.Accion == AccionPreviewFila.Omitir),
            filasRechazadas:     filas.Count(f => f.Accion == AccionPreviewFila.Rechazada),
            filasConAdvertencia: filas.Count(f =>
                f.Accion != AccionPreviewFila.Rechazada && f.Advertencias.Count > 0));

        importaciones.Agregar(importacion);
        await importaciones.GuardarCambiosAsync(cancellationToken);

        var muestraFilas = ConstruirMuestra(filas);

        return Result.Success(new PreviewImportacionDto(
            importacion.Id,
            request.NombreArchivo,
            TotalFilas: filas.Count,
            FilasACrear: filas.Count(f => f.Accion == AccionPreviewFila.Crear),
            FilasAActualizar: filas.Count(f => f.Accion == AccionPreviewFila.Actualizar),
            FilasAOmitir: filas.Count(f => f.Accion == AccionPreviewFila.Omitir),
            FilasRechazadas: filas.Count(f => f.Accion == AccionPreviewFila.Rechazada),
            FilasConAdvertencia: filas.Count(f =>
                f.Accion != AccionPreviewFila.Rechazada && f.Advertencias.Count > 0),
            MuestraFilas: muestraFilas));
    }

    private static List<FilaPreviewDto> ConstruirMuestra(IReadOnlyList<FilaPreviewDto> filas)
    {
        var vistos = new HashSet<int>();
        var muestra = new List<FilaPreviewDto>(MaxMuestraPorCategoria * 5);

        void Tomar(Func<FilaPreviewDto, bool> filtro)
        {
            foreach (var f in filas.Where(filtro).Take(MaxMuestraPorCategoria))
                if (vistos.Add(f.NumeroFila))
                    muestra.Add(f);
        }

        Tomar(f => f.Accion == AccionPreviewFila.Crear);
        Tomar(f => f.Accion == AccionPreviewFila.Actualizar);
        Tomar(f => f.Accion == AccionPreviewFila.Omitir);
        Tomar(f => f.Accion != AccionPreviewFila.Rechazada && f.Advertencias.Count > 0);
        Tomar(f => f.Accion == AccionPreviewFila.Rechazada);

        return muestra;
    }

    private static string SanitizarNombre(string nombre)
    {
        var sinExtension = Path.GetFileNameWithoutExtension(nombre);
        var invalidos = Path.GetInvalidFileNameChars();
        return new string(sinExtension.Select(c => invalidos.Contains(c) ? '_' : c).ToArray());
    }
}
