using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ImportacionDomain = SG.Domain.Importacion;

namespace SG.Infrastructure.Persistencia.Configurations.Importacion;

internal static class CapaVersionadaConfiguration
{
    public static void ConfigurarBase<T>(EntityTypeBuilder<T> builder, string tabla)
        where T : ImportacionDomain.CapaVersionada
    {
        builder.ToTable(tabla, "dominio");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DatasetVersionId).IsRequired();
        builder.Property(x => x.AtributosExtra)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");
        builder.Property(x => x.FilaOrigen).IsRequired();
        builder.HasOne<ImportacionDomain.DatasetVersion>()
            .WithMany()
            .HasForeignKey(x => x.DatasetVersionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.DatasetVersionId)
            .HasDatabaseName($"ix_{tabla}_dataset_version_id");
    }

    public static void ConfigurarSrid<T>(EntityTypeBuilder<T> builder, string tabla)
        where T : ImportacionDomain.CapaVersionada
    {
        builder.ToTable(tabla, "dominio", table =>
        {
            table.HasCheckConstraint($"ck_{tabla}_srid_32719", "ST_SRID(geometria) = 32719");
        });
    }
}

public sealed class CapaParcelaConfiguration : IEntityTypeConfiguration<ImportacionDomain.CapaParcela>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.CapaParcela> builder)
    {
        CapaVersionadaConfiguration.ConfigurarBase(builder, "capa_parcelas");
        CapaVersionadaConfiguration.ConfigurarSrid(builder, "capa_parcelas");
        builder.Property(x => x.Geometria).IsRequired().HasColumnType("geometry(Polygon,32719)");
        builder.Property(x => x.CodUv).IsRequired();
        builder.Property(x => x.CodMan).IsRequired();
        builder.Property(x => x.CodPred).IsRequired();
        builder.Property(x => x.CodigoGeografico).HasMaxLength(11);
        builder.Property(x => x.Superficie).HasColumnType("numeric(14,4)");
        builder.Property(x => x.TipoInmueble).HasMaxLength(10);
        builder.Property(x => x.ServicioAlcantarillado).HasMaxLength(2);
        builder.Property(x => x.ServicioAgua).HasMaxLength(2);
        builder.Property(x => x.ServicioLuz).HasMaxLength(2);
        builder.Property(x => x.ServicioTelefonia).HasMaxLength(2);
        builder.Property(x => x.NombrePropietarioOrigen).HasMaxLength(100);
        builder.Property(x => x.NombreVia).HasMaxLength(100);
        builder.Property(x => x.DireccionBarrio).HasMaxLength(100);
        builder.Property(x => x.DireccionUrbana).HasMaxLength(100);
        builder.Property(x => x.UsoTerreno).HasMaxLength(3);
        builder.Property(x => x.TopografiaTerreno).HasMaxLength(3);
        builder.HasIndex(x => new { x.DatasetVersionId, x.CodUv, x.CodMan, x.CodPred })
            .HasDatabaseName("ix_capa_parcelas_version_triplete");
        builder.HasIndex(x => x.Geometria)
            .HasMethod("gist")
            .HasFilter("geometria IS NOT NULL")
            .HasDatabaseName("ix_capa_parcelas_geometria");
    }
}

public sealed class CapaEdificacionConfiguration : IEntityTypeConfiguration<ImportacionDomain.CapaEdificacion>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.CapaEdificacion> builder)
    {
        CapaVersionadaConfiguration.ConfigurarBase(builder, "capa_edificaciones");
        CapaVersionadaConfiguration.ConfigurarSrid(builder, "capa_edificaciones");
        builder.Property(x => x.Geometria).HasColumnType("geometry(MultiPolygon,32719)");
        builder.Property(x => x.CodigoGeografico).HasMaxLength(11);
        builder.Property(x => x.CodigoEspacio).HasMaxLength(3);
        builder.Property(x => x.AreaConstruida).HasColumnType("numeric(14,4)");
        builder.HasIndex(x => x.Geometria)
            .HasMethod("gist")
            .HasFilter("geometria IS NOT NULL")
            .HasDatabaseName("ix_capa_edificaciones_geometria");
    }
}

public sealed class CapaPredioNoFotografiadoConfiguration : IEntityTypeConfiguration<ImportacionDomain.CapaPredioNoFotografiado>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.CapaPredioNoFotografiado> builder)
    {
        CapaVersionadaConfiguration.ConfigurarBase(builder, "capa_predios_no_fotografiados");
        CapaVersionadaConfiguration.ConfigurarSrid(builder, "capa_predios_no_fotografiados");
        builder.Property(x => x.Geometria).HasColumnType("geometry(MultiPolygon,32719)");
        builder.Property(x => x.CodigoGeografico).HasMaxLength(11);
        builder.Property(x => x.IndicadorFotos).HasMaxLength(2);
        builder.Property(x => x.FotoFrente).HasMaxLength(85);
        builder.Property(x => x.FotoDerecha).HasMaxLength(85);
        builder.Property(x => x.FotoIzquierda).HasMaxLength(85);
        builder.HasIndex(x => x.Geometria)
            .HasMethod("gist")
            .HasFilter("geometria IS NOT NULL")
            .HasDatabaseName("ix_capa_predios_no_fotografiados_geometria");
    }
}

public sealed class CapaManzanaConfiguration : IEntityTypeConfiguration<ImportacionDomain.CapaManzana>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.CapaManzana> builder)
    {
        CapaVersionadaConfiguration.ConfigurarBase(builder, "capa_manzanas");
        CapaVersionadaConfiguration.ConfigurarSrid(builder, "capa_manzanas");
        builder.Property(x => x.Geometria).HasColumnType("geometry(MultiPolygon,32719)");
        builder.Property(x => x.CodigoGeografico).HasMaxLength(11);
        builder.Property(x => x.CoordenadaOrigen).HasColumnType("numeric(5,1)");
        builder.HasIndex(x => x.Geometria)
            .HasMethod("gist")
            .HasFilter("geometria IS NOT NULL")
            .HasDatabaseName("ix_capa_manzanas_geometria");
    }
}

public sealed class CapaDistritoConfiguration : IEntityTypeConfiguration<ImportacionDomain.CapaDistrito>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.CapaDistrito> builder)
    {
        CapaVersionadaConfiguration.ConfigurarBase(builder, "capa_distritos");
        CapaVersionadaConfiguration.ConfigurarSrid(builder, "capa_distritos");
        builder.Property(x => x.Geometria).HasColumnType("geometry(MultiPolygon,32719)");
        builder.Property(x => x.CodigoGeografico).HasMaxLength(11);
        builder.Property(x => x.Nombre).HasMaxLength(30);
        builder.HasIndex(x => x.Geometria)
            .HasMethod("gist")
            .HasFilter("geometria IS NOT NULL")
            .HasDatabaseName("ix_capa_distritos_geometria");
    }
}

public sealed class CapaAreaUrbanaConfiguration : IEntityTypeConfiguration<ImportacionDomain.CapaAreaUrbana>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.CapaAreaUrbana> builder)
    {
        CapaVersionadaConfiguration.ConfigurarBase(builder, "capa_areas_urbanas");
        CapaVersionadaConfiguration.ConfigurarSrid(builder, "capa_areas_urbanas");
        builder.ToTable("capa_areas_urbanas", "dominio", table =>
            table.HasCheckConstraint(
                "ck_capa_areas_urbanas_tipo_geometria",
                "geometria IS NULL OR GeometryType(geometria) IN ('POLYGON', 'MULTIPOLYGON')"));
        builder.Property(x => x.Geometria).HasColumnType("geometry(Geometry,32719)");
        builder.HasIndex(x => x.Geometria).HasMethod("gist")
            .HasFilter("geometria IS NOT NULL")
            .HasDatabaseName("ix_capa_areas_urbanas_geometria");
    }
}

public sealed class CapaPuntoGeodesicoConfiguration : IEntityTypeConfiguration<ImportacionDomain.CapaPuntoGeodesico>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.CapaPuntoGeodesico> builder)
    {
        CapaVersionadaConfiguration.ConfigurarBase(builder, "capa_puntos_geodesicos");
        CapaVersionadaConfiguration.ConfigurarSrid(builder, "capa_puntos_geodesicos");
        builder.Property(x => x.Geometria).HasColumnType("geometry(Point,32719)");
        builder.HasIndex(x => x.Geometria).HasMethod("gist")
            .HasFilter("geometria IS NOT NULL")
            .HasDatabaseName("ix_capa_puntos_geodesicos_geometria");
    }
}

public sealed class CapaZonaConfiguration : IEntityTypeConfiguration<ImportacionDomain.CapaZona>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.CapaZona> builder)
    {
        CapaVersionadaConfiguration.ConfigurarBase(builder, "capa_zonas");
        CapaVersionadaConfiguration.ConfigurarSrid(builder, "capa_zonas");
        builder.Property(x => x.Geometria).HasColumnType("geometry(MultiPolygon,32719)");
        builder.Property(x => x.NombreZona).HasMaxLength(254);
        builder.Property(x => x.CodigoGeografico).HasMaxLength(11);
        builder.HasIndex(x => x.Geometria)
            .HasMethod("gist")
            .HasFilter("geometria IS NOT NULL")
            .HasDatabaseName("ix_capa_zonas_geometria");
    }
}

public sealed class CapaViaConfiguration : IEntityTypeConfiguration<ImportacionDomain.CapaVia>
{
    public void Configure(EntityTypeBuilder<ImportacionDomain.CapaVia> builder)
    {
        CapaVersionadaConfiguration.ConfigurarBase(builder, "capa_vias");
        CapaVersionadaConfiguration.ConfigurarSrid(builder, "capa_vias");
        builder.Property(x => x.Geometria).HasColumnType("geometry(MultiLineString,32719)");
        builder.Property(x => x.Material).HasMaxLength(7);
        builder.Property(x => x.Nombre).HasMaxLength(50);
        builder.Property(x => x.Tipo).HasMaxLength(50);
        builder.Property(x => x.DistanciaOrigen).HasColumnType("numeric(19,11)");
        builder.HasIndex(x => x.Geometria)
            .HasMethod("gist")
            .HasFilter("geometria IS NOT NULL")
            .HasDatabaseName("ix_capa_vias_geometria");
    }
}
