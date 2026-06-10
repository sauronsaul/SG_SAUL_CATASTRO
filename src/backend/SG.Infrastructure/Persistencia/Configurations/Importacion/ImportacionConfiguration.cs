using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Infrastructure.Persistencia.Configurations.Importacion;

public class ImportacionConfiguration : IEntityTypeConfiguration<ImportacionDomain.Importacion>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.Importacion> builder)
    {
        builder.ToTable("importaciones", schema: "dominio");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.PerfilId).IsRequired();

        builder.Property(x => x.NombreArchivo)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.RutaMinioZip)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.FechaImportacion)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.ImportadoPorId).IsRequired();

        builder.Property(x => x.TotalFilas).IsRequired();

        builder.Property(x => x.FilasEstimadasACrear)
            .IsRequired().HasColumnName("filas_estimadas_a_crear");
        builder.Property(x => x.FilasEstimadasAActualizar)
            .IsRequired().HasColumnName("filas_estimadas_a_actualizar");
        builder.Property(x => x.FilasEstimadasAOmitir)
            .IsRequired().HasColumnName("filas_estimadas_a_omitir");
        builder.Property(x => x.FilasEstimadasRechazadas)
            .IsRequired().HasColumnName("filas_estimadas_rechazadas");
        builder.Property(x => x.FilasEstimadasConAdvertencia)
            .IsRequired().HasColumnName("filas_estimadas_con_advertencia");

        builder.Property(x => x.FilasCreadas)
            .IsRequired().HasColumnName("filas_creadas");
        builder.Property(x => x.FilasActualizadas)
            .IsRequired().HasColumnName("filas_actualizadas");
        builder.Property(x => x.FilasOmitidas)
            .IsRequired().HasColumnName("filas_omitidas");
        builder.Property(x => x.FilasRechazadas)
            .IsRequired().HasColumnName("filas_rechazadas");
        builder.Property(x => x.FilasConAdvertencia)
            .IsRequired().HasColumnName("filas_con_advertencia");

        builder.Property(x => x.Estado)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(x => x.CreatedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.UpdatedAt)
            .HasColumnType("timestamptz");

        builder.HasIndex(x => x.PerfilId)
            .HasDatabaseName("ix_importaciones_perfil_id");

        builder.HasIndex(x => x.FechaImportacion)
            .HasDatabaseName("ix_importaciones_fecha");
    }
}
