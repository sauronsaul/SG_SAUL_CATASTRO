using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SG.Domain.Catastro;

namespace SG.Infrastructure.Persistencia.Configurations.Catastro;

public class RelacionPredioPropietarioConfiguration : IEntityTypeConfiguration<RelacionPredioPropietario>
{
    public void Configure(EntityTypeBuilder<RelacionPredioPropietario> builder)
    {
        builder.ToTable("relaciones_predio_propietario", schema: "dominio");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.PredioId).IsRequired();
        builder.Property(x => x.PropietarioId).IsRequired();

        builder.Property(x => x.TipoDerecho)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.Porcentaje)
            .IsRequired()
            .HasColumnType("numeric(5,2)");

        builder.Property(x => x.VigenteDesde)
            .IsRequired()
            .HasColumnType("date");

        builder.Property(x => x.VigenteHasta)
            .HasColumnType("date");

        builder.Property(x => x.CreadoPor).IsRequired();

        builder.Property(x => x.CreadoAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Ignore(x => x.EsVigente);

        builder.HasIndex(x => new { x.PredioId, x.PropietarioId, x.VigenteDesde })
            .HasDatabaseName("ix_relaciones_predio_propietario_predio_prop_desde");

        // FK a propietarios — Restrict (no eliminar propietario con relaciones vigentes).
        builder.HasOne<Propietario>()
            .WithMany()
            .HasForeignKey(x => x.PropietarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
