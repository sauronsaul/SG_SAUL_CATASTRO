using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Importacion;

namespace SG.Infrastructure.Persistencia.Configurations.Importacion;

public class MapeoColumnaConfiguration : IEntityTypeConfiguration<MapeoColumna>
{
    public void Configure(EntityTypeBuilder<MapeoColumna> builder)
    {
        builder.ToTable("mapeos_columna", schema: "dominio");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.PerfilImportacionId).IsRequired();

        builder.Property(x => x.NombreColumnaOrigen)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.CampoDestino)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.EsObligatorio).IsRequired();

        builder.HasIndex(x => new { x.PerfilImportacionId, x.NombreColumnaOrigen })
            .IsUnique()
            .HasDatabaseName("uix_mapeos_columna_perfil_origen");

        builder.HasMany(x => x.Equivalencias)
            .WithOne()
            .HasForeignKey(e => e.MapeoColumnaId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
