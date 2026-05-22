using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Catastro;

namespace SG.Infrastructure.Persistencia.Configurations.Catastro;

/// <summary>
/// HistorialEstado es INMUTABLE: no tiene updated_at, is_deleted ni ninguna modificación. Ver ADR 0030.
/// </summary>
public class HistorialEstadoConfiguration : IEntityTypeConfiguration<HistorialEstado>
{
    public void Configure(EntityTypeBuilder<HistorialEstado> builder)
    {
        builder.ToTable("historial_estados", schema: "dominio");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.PredioId).IsRequired();

        builder.Property(x => x.EstadoAnterior)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.EstadoNuevo)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.CambiadoPor).IsRequired();

        builder.Property(x => x.CambiadoAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.Observaciones)
            .HasMaxLength(1000);

        builder.HasIndex(x => x.PredioId)
            .HasDatabaseName("ix_historial_estados_predio_id");
    }
}
