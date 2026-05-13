using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Infrastructure.Identidad;

namespace SG.Infrastructure.Persistencia.Configurations;

public class UsuarioIdentidadConfiguration : IEntityTypeConfiguration<UsuarioIdentidad>
{
    public void Configure(EntityTypeBuilder<UsuarioIdentidad> builder)
    {
        builder.ToTable("usuarios", schema: "identidad");

        builder.Property(x => x.NombreCompleto)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.NormalizedEmail).HasMaxLength(320);
        builder.Property(x => x.UserName).HasMaxLength(320);
        builder.Property(x => x.NormalizedUserName).HasMaxLength(320);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.IsDeleted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(x => x.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ix_usuarios_normalized_email");

        builder.HasIndex(x => x.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("ix_usuarios_normalized_user_name");
    }
}
