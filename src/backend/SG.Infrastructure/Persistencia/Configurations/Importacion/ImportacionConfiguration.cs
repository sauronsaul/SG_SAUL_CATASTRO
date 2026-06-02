using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Infrastructure.Persistencia.Configurations.Importacion;

public class ImportacionConfiguration : IEntityTypeConfiguration<ImportacionDomain.Importacion>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.Importacion> builder)
    {
        builder.ToTable("importaciones", schema: "dominio");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PerfilId).IsRequired();

        builder.Property(x => x.NombreArchivo)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.RutaMinioZip)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.FechaImportacion)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.ImportadoPorId).IsRequired();

        builder.Property(x => x.TotalFilas).IsRequired();
        builder.Property(x => x.FilasImportadas).IsRequired();
        builder.Property(x => x.FilasConAdvertencia).IsRequired();
        builder.Property(x => x.FilasRechazadas).IsRequired();
        builder.Property(x => x.FilasOmitidas).IsRequired();

        builder.Property(x => x.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.HasIndex(x => x.PerfilId)
            .HasDatabaseName("ix_importaciones_perfil_id");

        builder.HasIndex(x => x.FechaImportacion)
            .HasDatabaseName("ix_importaciones_fecha");
    }
}
