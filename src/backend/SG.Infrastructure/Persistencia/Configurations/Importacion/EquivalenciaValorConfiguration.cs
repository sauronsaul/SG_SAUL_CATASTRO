using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Importacion;

namespace SG.Infrastructure.Persistencia.Configurations.Importacion;

public class EquivalenciaValorConfiguration : IEntityTypeConfiguration<EquivalenciaValor>
{
    public void Configure(EntityTypeBuilder<EquivalenciaValor> builder)
    {
        builder.ToTable("equivalencias_valor", schema: "dominio");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.MapeoColumnaId).IsRequired();

        builder.Property(x => x.ValorOrigen)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ValorDestino)
            .IsRequired()
            .HasMaxLength(200);

        builder.HasIndex(x => new { x.MapeoColumnaId, x.ValorOrigen })
            .IsUnique()
            .HasDatabaseName("uix_equivalencias_valor_mapeo_origen");
    }
}
