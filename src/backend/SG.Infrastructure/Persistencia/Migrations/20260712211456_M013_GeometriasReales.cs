using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M013_GeometriasReales : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM dominio.capa_edificaciones)
                       OR EXISTS (SELECT 1 FROM dominio.capa_predios_no_fotografiados)
                       OR EXISTS (SELECT 1 FROM dominio.capa_manzanas)
                       OR EXISTS (SELECT 1 FROM dominio.capa_distritos)
                       OR EXISTS (SELECT 1 FROM dominio.capa_zonas)
                       OR EXISTS (SELECT 1 FROM dominio.capa_vias) THEN
                        RAISE EXCEPTION
                            'M013 requiere vacías las seis tablas de capas auxiliares; descarte o purgue sus versiones antes de migrar.';
                    END IF;
                END $$;

                ALTER TABLE dominio.capa_edificaciones
                    ALTER COLUMN geometria DROP NOT NULL,
                    ALTER COLUMN geometria TYPE geometry(MultiPolygon,32719)
                        USING ST_Multi(geometria)::geometry(MultiPolygon,32719);
                ALTER TABLE dominio.capa_predios_no_fotografiados
                    ALTER COLUMN geometria DROP NOT NULL,
                    ALTER COLUMN geometria TYPE geometry(MultiPolygon,32719)
                        USING ST_Multi(geometria)::geometry(MultiPolygon,32719);
                ALTER TABLE dominio.capa_manzanas
                    ALTER COLUMN geometria DROP NOT NULL,
                    ALTER COLUMN geometria TYPE geometry(MultiPolygon,32719)
                        USING ST_Multi(geometria)::geometry(MultiPolygon,32719);
                ALTER TABLE dominio.capa_distritos
                    ALTER COLUMN geometria DROP NOT NULL,
                    ALTER COLUMN geometria TYPE geometry(MultiPolygon,32719)
                        USING ST_Multi(geometria)::geometry(MultiPolygon,32719);
                ALTER TABLE dominio.capa_zonas
                    ALTER COLUMN geometria DROP NOT NULL,
                    ALTER COLUMN geometria TYPE geometry(MultiPolygon,32719)
                        USING ST_Multi(geometria)::geometry(MultiPolygon,32719);
                ALTER TABLE dominio.capa_vias
                    ALTER COLUMN geometria DROP NOT NULL,
                    ALTER COLUMN geometria TYPE geometry(MultiLineString,32719)
                        USING ST_Multi(geometria)::geometry(MultiLineString,32719);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM dominio.capa_edificaciones
                        WHERE geometria IS NULL OR ST_NumGeometries(geometria) <> 1)
                       OR EXISTS (
                        SELECT 1 FROM dominio.capa_predios_no_fotografiados
                        WHERE geometria IS NULL OR ST_NumGeometries(geometria) <> 1)
                       OR EXISTS (
                        SELECT 1 FROM dominio.capa_manzanas
                        WHERE geometria IS NULL OR ST_NumGeometries(geometria) <> 1)
                       OR EXISTS (
                        SELECT 1 FROM dominio.capa_distritos
                        WHERE geometria IS NULL OR ST_NumGeometries(geometria) <> 1)
                       OR EXISTS (
                        SELECT 1 FROM dominio.capa_zonas
                        WHERE geometria IS NULL OR ST_NumGeometries(geometria) <> 1)
                       OR EXISTS (
                        SELECT 1 FROM dominio.capa_vias
                        WHERE geometria IS NULL OR ST_NumGeometries(geometria) <> 1) THEN
                        RAISE EXCEPTION
                            'No se puede revertir M013: existen geometrías nulas o multipartes en capas auxiliares.';
                    END IF;
                END $$;

                ALTER TABLE dominio.capa_edificaciones
                    ALTER COLUMN geometria TYPE geometry(Polygon,32719)
                        USING ST_GeometryN(geometria, 1)::geometry(Polygon,32719),
                    ALTER COLUMN geometria SET NOT NULL;
                ALTER TABLE dominio.capa_predios_no_fotografiados
                    ALTER COLUMN geometria TYPE geometry(Polygon,32719)
                        USING ST_GeometryN(geometria, 1)::geometry(Polygon,32719),
                    ALTER COLUMN geometria SET NOT NULL;
                ALTER TABLE dominio.capa_manzanas
                    ALTER COLUMN geometria TYPE geometry(Polygon,32719)
                        USING ST_GeometryN(geometria, 1)::geometry(Polygon,32719),
                    ALTER COLUMN geometria SET NOT NULL;
                ALTER TABLE dominio.capa_distritos
                    ALTER COLUMN geometria TYPE geometry(Polygon,32719)
                        USING ST_GeometryN(geometria, 1)::geometry(Polygon,32719),
                    ALTER COLUMN geometria SET NOT NULL;
                ALTER TABLE dominio.capa_zonas
                    ALTER COLUMN geometria TYPE geometry(Polygon,32719)
                        USING ST_GeometryN(geometria, 1)::geometry(Polygon,32719),
                    ALTER COLUMN geometria SET NOT NULL;
                ALTER TABLE dominio.capa_vias
                    ALTER COLUMN geometria TYPE geometry(LineString,32719)
                        USING ST_GeometryN(geometria, 1)::geometry(LineString,32719),
                    ALTER COLUMN geometria SET NOT NULL;
                """);
        }
    }
}
