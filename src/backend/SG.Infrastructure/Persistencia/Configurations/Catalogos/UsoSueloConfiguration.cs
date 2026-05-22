using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Catalogos;

namespace SG.Infrastructure.Persistencia.Configurations.Catalogos;

public class UsoSueloConfiguration : IEntityTypeConfiguration<UsoSuelo>
{
    public void Configure(EntityTypeBuilder<UsoSuelo> builder)
    {
        builder.ToTable("catalogo_uso_suelo", schema: "dominio");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Codigo)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(x => x.Nombre)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Descripcion)
            .HasMaxLength(300);

        builder.Property(x => x.Activo)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(x => x.Orden)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.HasIndex(x => x.Codigo)
            .IsUnique()
            .HasDatabaseName("uix_usos_suelo_codigo");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
