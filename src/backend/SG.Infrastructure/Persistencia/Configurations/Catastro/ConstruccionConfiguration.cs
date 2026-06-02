using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Catastro;

namespace SG.Infrastructure.Persistencia.Configurations.Catastro;

public class ConstruccionConfiguration : IEntityTypeConfiguration<Construccion>
{
    public void Configure(EntityTypeBuilder<Construccion> builder)
    {
        builder.ToTable("construcciones", schema: "dominio");

        builder.HasKey(x => x.Id);
        // PK generado por el dominio en el constructor — ADR 0033.
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.PredioId).IsRequired();

        builder.Property(x => x.Numero).IsRequired();

        builder.Property(x => x.Pisos).IsRequired();

        builder.Property(x => x.Bloque)
            .HasMaxLength(50);

        builder.Property(x => x.AreaConstruida)
            .IsRequired()
            .HasColumnType("numeric(14,4)");

        builder.Property(x => x.TipoConstruccion)
            .HasMaxLength(100);

        builder.HasIndex(x => x.PredioId)
            .HasDatabaseName("ix_construcciones_predio_id");
    }
}
