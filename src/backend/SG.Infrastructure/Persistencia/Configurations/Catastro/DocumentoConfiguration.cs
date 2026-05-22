using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Catastro;

namespace SG.Infrastructure.Persistencia.Configurations.Catastro;

public class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("documentos", schema: "dominio");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.PredioId).IsRequired();

        builder.Property(x => x.NombreArchivo)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.SizeBytes).IsRequired();

        builder.Property(x => x.MinioKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.TipoDocumento)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.SubidoPor).IsRequired();

        builder.Property(x => x.SubidoAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.EliminadoAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.MotivoEliminacion)
            .HasMaxLength(500);

        // No HasQueryFilter: se necesita acceso a documentos eliminados para auditoría.
        // Filtrar IsDeleted en las consultas normales explícitamente.
    }
}
