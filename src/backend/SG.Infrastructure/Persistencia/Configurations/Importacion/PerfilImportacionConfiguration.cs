using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Importacion;

namespace SG.Infrastructure.Persistencia.Configurations.Importacion;

public class PerfilImportacionConfiguration : IEntityTypeConfiguration<PerfilImportacion>
{
    public void Configure(EntityTypeBuilder<PerfilImportacion> builder)
    {
        builder.ToTable("perfiles_importacion", schema: "dominio");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.Nombre)
            .IsUnique()
            .HasDatabaseName("uix_perfiles_importacion_nombre");

        builder.Property(x => x.Descripcion)
            .HasMaxLength(500);

        builder.Property(x => x.TipoCapa)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.NombreArchivoShp)
            .IsRequired()
            .HasMaxLength(260);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.HasMany(x => x.Mapeos)
            .WithOne()
            .HasForeignKey(m => m.PerfilImportacionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
