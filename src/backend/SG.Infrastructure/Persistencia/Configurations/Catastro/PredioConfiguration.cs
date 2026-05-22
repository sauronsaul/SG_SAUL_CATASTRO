using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;
using SG.Domain.Catastro;
using SG.Domain.Catastro.Enums;
using SG.Domain.Catastro.ValueObjects;

namespace SG.Infrastructure.Persistencia.Configurations.Catastro;

public class PredioConfiguration : IEntityTypeConfiguration<Predio>
{
    public void Configure(EntityTypeBuilder<Predio> builder)
    {
        builder.ToTable("predios", schema: "dominio");

        builder.HasKey(x => x.Id);

        // CodigoCatastral — nullable hasta Validar(); value converter a string canónica.
        builder.Property(x => x.CodigoCatastral)
            .HasConversion(
                v => v != null ? v.Valor : null,
                v => v != null ? CodigoCatastral.FromDb(v) : null)
            .HasColumnName("codigo_catastral")
            .HasMaxLength(24);

        // UbicacionCatastral — owned inline.
        builder.OwnsOne(x => x.Ubicacion, ub =>
        {
            ub.Property(u => u.Zona).IsRequired().HasMaxLength(50).HasColumnName("ubic_zona");
            ub.Property(u => u.Manzana).IsRequired().HasMaxLength(20).HasColumnName("ubic_manzana");
            ub.Property(u => u.Lote).IsRequired().HasMaxLength(20).HasColumnName("ubic_lote");
            ub.Property(u => u.Barrio).HasMaxLength(100).HasColumnName("ubic_barrio");
            ub.Property(u => u.Direccion).HasMaxLength(300).HasColumnName("ubic_direccion");
            ub.Property(u => u.Referencia).HasMaxLength(300).HasColumnName("ubic_referencia");
        });

        builder.Property(x => x.SuperficieDeclarada)
            .IsRequired()
            .HasColumnType("numeric(14,4)");

        builder.Property(x => x.SuperficieSig)
            .HasColumnType("numeric(14,4)");

        builder.Property(x => x.SuperficieOficial)
            .HasColumnType("numeric(14,4)");

        builder.Property(x => x.UsoSueloId)
            .IsRequired();

        builder.Property(x => x.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        // GeometriaPredial — owned nullable inline.
        builder.OwnsOne(x => x.Geometria, geo =>
        {
            geo.Property(g => g.Poligono)
                .HasColumnName("geometria")
                .HasColumnType("geometry(Polygon, 32719)");
        });
        builder.Navigation(x => x.Geometria).IsRequired(false);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.Property(x => x.RowVersion)
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasIndex(x => x.CodigoCatastral)
            .IsUnique()
            .HasDatabaseName("uix_predios_codigo_catastral");

        // UsoSuelo FK — sin cascade delete (catálogo estable).
        builder.HasOne<Domain.Catalogos.UsoSuelo>()
            .WithMany()
            .HasForeignKey(x => x.UsoSueloId)
            .OnDelete(DeleteBehavior.Restrict);

        // Colecciones navegadas.
        builder.HasMany(x => x.Relaciones)
            .WithOne()
            .HasForeignKey(r => r.PredioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Documentos)
            .WithOne()
            .HasForeignKey(d => d.PredioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Historial)
            .WithOne()
            .HasForeignKey(h => h.PredioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
