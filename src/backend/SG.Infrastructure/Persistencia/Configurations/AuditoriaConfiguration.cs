using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Infrastructure.Auditoria;

namespace SG.Infrastructure.Persistencia.Configurations;

public class AuditoriaConfiguration : IEntityTypeConfiguration<AuditoriaEntidad>
{
    public void Configure(EntityTypeBuilder<AuditoriaEntidad> builder)
    {
        builder.ToTable("auditoria", schema: "auditoria");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Timestamp)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.Modulo)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Accion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.EntidadTipo)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.EntidadId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ValorAnterior)
            .HasColumnType("jsonb");

        builder.Property(x => x.ValorNuevo)
            .HasColumnType("jsonb");

        builder.Property(x => x.Resultado)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("OK");

        builder.Property(x => x.IpOrigen)
            .HasMaxLength(45);

        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => new { x.EntidadTipo, x.EntidadId });
    }
}
