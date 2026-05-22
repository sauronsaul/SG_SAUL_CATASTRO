using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Catastro;

namespace SG.Infrastructure.Persistencia.Configurations.Catastro;

public class PropietarioConfiguration : IEntityTypeConfiguration<Propietario>
{
    public void Configure(EntityTypeBuilder<Propietario> builder)
    {
        builder.ToTable("propietarios", schema: "dominio");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Tipo)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        // PersonaNatural
        builder.Property(x => x.Nombre).HasMaxLength(100);
        builder.Property(x => x.Apellidos).HasMaxLength(100);
        builder.Property(x => x.Cedula).HasMaxLength(15);

        // PersonaJuridica
        builder.Property(x => x.RazonSocial).HasMaxLength(200);
        builder.Property(x => x.Nit).HasMaxLength(13);
        builder.Property(x => x.RepresentanteLegal).HasMaxLength(200);

        // Comunes
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.Telefono).HasMaxLength(20);
        builder.Property(x => x.Direccion).HasMaxLength(500);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.RowVersion)
            .IsRowVersion();

        builder.Ignore(x => x.NombreCompleto);

        builder.HasIndex(x => x.Cedula)
            .HasDatabaseName("ix_propietarios_cedula");

        builder.HasIndex(x => x.Nit)
            .HasDatabaseName("ix_propietarios_nit");

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
