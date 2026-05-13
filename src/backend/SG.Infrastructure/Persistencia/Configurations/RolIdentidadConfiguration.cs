using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Infrastructure.Identidad;

namespace SG.Infrastructure.Persistencia.Configurations;

public class RolIdentidadConfiguration : IEntityTypeConfiguration<RolIdentidad>
{
    public void Configure(EntityTypeBuilder<RolIdentidad> builder)
    {
        builder.ToTable("roles", schema: "identidad");

        builder.Property(x => x.Descripcion)
            .HasMaxLength(500);

        builder.Property(x => x.Name).HasMaxLength(256);
        builder.Property(x => x.NormalizedName).HasMaxLength(256);

        builder.HasIndex(x => x.NormalizedName)
            .IsUnique()
            .HasDatabaseName("ix_roles_normalized_name");
    }
}
