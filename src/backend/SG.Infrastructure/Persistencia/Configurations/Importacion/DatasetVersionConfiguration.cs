using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Infrastructure.Persistencia.Configurations.Importacion;

public sealed class DatasetVersionConfiguration : IEntityTypeConfiguration<ImportacionDomain.DatasetVersion>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.DatasetVersion> builder)
    {
        builder.ToTable("dataset_versiones", "dominio", table =>
        {
            table.HasCheckConstraint("ck_dataset_versiones_numero_positivo", "numero_version > 0");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.NumeroVersion).IsRequired();
        builder.Property(x => x.MunicipioCodigo).IsRequired().HasMaxLength(30);
        builder.Property(x => x.OrigenDescripcion).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Estado).IsRequired().HasConversion<string>().HasMaxLength(30);
        builder.Property(x => x.RutaMinioPaquete).HasMaxLength(500);
        builder.Property(x => x.ReportePreliminar)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");
        builder.Property(x => x.ErrorCarga).HasMaxLength(2000);
        builder.Property(x => x.CreatedAt).IsRequired().HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.ActivadoAt).HasColumnType("timestamptz");
        builder.Property(x => x.ArchivadoAt).HasColumnType("timestamptz");

        builder.HasOne<ImportacionDomain.Importacion>()
            .WithMany()
            .HasForeignKey(x => x.ImportacionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.MunicipioCodigo, x.NumeroVersion })
            .IsUnique()
            .HasDatabaseName("uix_dataset_versiones_municipio_numero");

        builder.HasIndex(x => x.MunicipioCodigo)
            .IsUnique()
            .HasFilter("estado = 'Activa'")
            .HasDatabaseName("uix_dataset_versiones_municipio_activa");
    }
}
