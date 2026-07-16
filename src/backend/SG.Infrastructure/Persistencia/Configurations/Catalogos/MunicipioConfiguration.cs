using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Catalogos;

namespace SG.Infrastructure.Persistencia.Configurations.Catalogos;

public sealed class MunicipioConfiguration : IEntityTypeConfiguration<Municipio>
{
    public void Configure(EntityTypeBuilder<Municipio> builder)
    {
        builder.ToTable("municipios", "dominio", table =>
            table.HasCheckConstraint("ck_municipios_codigo_ine", "codigo_ine ~ '^[0-9]{6}$'"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodigoIne).IsRequired().HasMaxLength(6);
        builder.Property(x => x.Nombre).IsRequired().HasMaxLength(100);
        builder.Property(x => x.NombreOficial).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Departamento).IsRequired().HasMaxLength(50);
        builder.Property(x => x.FuenteCodigo).IsRequired().HasMaxLength(300);
        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.RowVersion)
            .HasColumnName("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        builder.HasAlternateKey(x => x.CodigoIne).HasName("ak_municipios_codigo_ine");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
