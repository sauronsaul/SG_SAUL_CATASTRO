using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Catalogos;
using SG.Domain.Importacion;

namespace SG.Infrastructure.Persistencia.Configurations.Importacion;

public sealed class EsquemaCapaMunicipioConfiguration : IEntityTypeConfiguration<EsquemaCapaMunicipio>
{
    public void Configure(EntityTypeBuilder<EsquemaCapaMunicipio> builder)
    {
        builder.ToTable("esquemas_capas", "dominio");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.MunicipioCodigo).IsRequired().HasMaxLength(6);
        builder.Property(x => x.TipoCapa).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.NombrePerfil).IsRequired().HasMaxLength(100);
        builder.Property(x => x.NombreArchivoShp).IsRequired().HasMaxLength(260);
        builder.Property(x => x.TablaDestino).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Obligatoria).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.RowVersion)
            .HasColumnName("xmin").HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
        builder.HasOne<Municipio>().WithMany()
            .HasForeignKey(x => x.MunicipioCodigo)
            .HasPrincipalKey(x => x.CodigoIne)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MunicipioCodigo, x.TipoCapa }).IsUnique()
            .HasDatabaseName("uix_esquemas_capas_municipio_tipo");
        builder.HasIndex(x => new { x.MunicipioCodigo, x.NombrePerfil }).IsUnique()
            .HasDatabaseName("uix_esquemas_capas_municipio_perfil");
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
