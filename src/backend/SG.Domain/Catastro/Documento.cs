using SG.Domain.Catastro.Enums;
using SG.Domain.Common;

namespace SG.Domain.Catastro;

/// <summary>
/// Documento adjunto al predio. Solo soft delete — nunca se elimina físicamente de la BD ni de MinIO.
/// El minioKey del archivo eliminado se mueve a papelera/ en MinIO. Ver ADR 0030.
/// </summary>
public sealed class Documento
{
    public Guid Id { get; private set; }
    public Guid PredioId { get; private set; }
    public string NombreArchivo { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string MinioKey { get; private set; } = string.Empty;
    public TipoDocumento TipoDocumento { get; private set; }
    public Guid SubidoPor { get; private set; }
    public DateTime SubidoAt { get; private set; }

    // Soft delete
    public bool IsDeleted { get; private set; }
    public DateTime? EliminadoAt { get; private set; }
    public Guid? EliminadoPor { get; private set; }
    public string? MotivoEliminacion { get; private set; }

    private Documento() { }

    internal static Documento Crear(
        Guid predioId,
        string nombreArchivo,
        string contentType,
        long sizeBytes,
        string minioKey,
        TipoDocumento tipoDocumento,
        Guid subidoPor)
    {
        return new Documento
        {
            Id = Guid.NewGuid(),
            PredioId = predioId,
            NombreArchivo = nombreArchivo.Trim(),
            ContentType = contentType.Trim(),
            SizeBytes = sizeBytes,
            MinioKey = minioKey.Trim(),
            TipoDocumento = tipoDocumento,
            SubidoPor = subidoPor,
            SubidoAt = DateTime.UtcNow,
            IsDeleted = false,
        };
    }

    internal Result Eliminar(Guid eliminadoPor, string motivo)
    {
        if (IsDeleted)
            return Result.Failure(DocumentoErrores.YaEliminado);

        if (string.IsNullOrWhiteSpace(motivo))
            return Result.Failure(DocumentoErrores.MotivoRequerido);

        IsDeleted = true;
        EliminadoAt = DateTime.UtcNow;
        EliminadoPor = eliminadoPor;
        MotivoEliminacion = motivo.Trim();
        return Result.Success();
    }
}

public static class DocumentoErrores
{
    public static readonly DomainError NoEncontrado = new("Documento.NoEncontrado", "El documento no fue encontrado.");
    public static readonly DomainError YaEliminado = new("Documento.YaEliminado", "El documento ya fue eliminado.");
    public static readonly DomainError MotivoRequerido = new("Documento.MotivoRequerido", "Se requiere un motivo para eliminar el documento.");
}
