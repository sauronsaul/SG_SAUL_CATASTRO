using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M015_NucleoMultiMunicipioGeocodigoIne : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uix_predios_triplete_activo",
                schema: "dominio",
                table: "predios");

            migrationBuilder.AddColumn<string>(
                name: "municipio_codigo",
                schema: "dominio",
                table: "predios",
                type: "character varying(6)",
                maxLength: 6,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "municipio_codigo",
                schema: "dominio",
                table: "dataset_versiones",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.CreateTable(
                name: "capa_areas_urbanas",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geometria = table.Column<Geometry>(type: "geometry(Geometry,32719)", nullable: true),
                    dataset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atributos_extra = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    fila_origen = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capa_areas_urbanas", x => x.id);
                    table.CheckConstraint("ck_capa_areas_urbanas_srid_32719", "ST_SRID(geometria) = 32719");
                    table.CheckConstraint("ck_capa_areas_urbanas_tipo_geometria", "geometria IS NULL OR GeometryType(geometria) IN ('POLYGON', 'MULTIPOLYGON')");
                    table.ForeignKey(
                        name: "fk_capa_areas_urbanas_dataset_versiones_dataset_version_id",
                        column: x => x.dataset_version_id,
                        principalSchema: "dominio",
                        principalTable: "dataset_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "capa_puntos_geodesicos",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geometria = table.Column<Point>(type: "geometry(Point,32719)", nullable: true),
                    dataset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atributos_extra = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    fila_origen = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capa_puntos_geodesicos", x => x.id);
                    table.CheckConstraint("ck_capa_puntos_geodesicos_srid_32719", "ST_SRID(geometria) = 32719");
                    table.ForeignKey(
                        name: "fk_capa_puntos_geodesicos_dataset_versiones_dataset_version_id",
                        column: x => x.dataset_version_id,
                        principalSchema: "dominio",
                        principalTable: "dataset_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "municipios",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_ine = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nombre_oficial = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    departamento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    fuente_codigo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipios", x => x.id);
                    table.UniqueConstraint("ak_municipios_codigo_ine", x => x.codigo_ine);
                    table.CheckConstraint("ck_municipios_codigo_ine", "codigo_ine ~ '^[0-9]{6}$'");
                });

            migrationBuilder.Sql("""
                INSERT INTO dominio.municipios
                    (id, codigo_ine, nombre, nombre_oficial, departamento, fuente_codigo,
                     created_at, is_deleted)
                VALUES
                    ('05120100-0000-0000-0000-000000000001', '051201', 'Uyuni',
                     'GOBIERNO AUTÓNOMO MUNICIPAL DE UYUNI', 'Potosí',
                     'INE Bolivia — Clasificación de Ubicación Geográfica (geocódigo DDPPMM). Corroborado en publicaciones oficiales INE (visorPdf Codigo=051201; serie AtlasMunicipal nombrada por geocódigo). Verificado 2026-07-16.',
                     NOW(), false),
                    ('02200100-0000-0000-0000-000000000001', '022001', 'Caranavi',
                     'GOBIERNO AUTÓNOMO MUNICIPAL DE CARANAVI', 'La Paz',
                     'INE Bolivia — Clasificación de Ubicación Geográfica (geocódigo DDPPMM). Corroborado en publicaciones oficiales INE (visorPdf Codigo=051201; serie AtlasMunicipal nombrada por geocódigo). Verificado 2026-07-16.',
                     NOW(), false)
                ON CONFLICT (codigo_ine) DO NOTHING;

                UPDATE dominio.dataset_versiones
                SET municipio_codigo = '051201'
                WHERE municipio_codigo = 'UYUNI';

                UPDATE dominio.predios
                SET municipio_codigo = '051201'
                WHERE municipio_codigo IS NULL;
                """);

            migrationBuilder.AlterColumn<string>(
                name: "municipio_codigo",
                schema: "dominio",
                table: "predios",
                type: "character varying(6)",
                maxLength: 6,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(6)",
                oldMaxLength: 6,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "esquemas_capas",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    municipio_codigo = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    tipo_capa = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre_perfil = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    nombre_archivo_shp = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    tabla_destino = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    obligatoria = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_esquemas_capas", x => x.id);
                    table.ForeignKey(
                        name: "fk_esquemas_capas_municipios_municipio_codigo",
                        column: x => x.municipio_codigo,
                        principalSchema: "dominio",
                        principalTable: "municipios",
                        principalColumn: "codigo_ine",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO dominio.esquemas_capas
                    (id, municipio_codigo, tipo_capa, nombre_perfil, nombre_archivo_shp,
                     tabla_destino, obligatoria, created_at, is_deleted)
                VALUES
                    ('05120100-0000-0000-0000-000000000101', '051201', 'Predios', 'uyuni-versionado-parcelas', 'PRE_SIS_UYU.shp', 'capa_parcelas', true, NOW(), false),
                    ('05120100-0000-0000-0000-000000000102', '051201', 'Construcciones', 'uyuni-versionado-edificaciones', 'EDI_SIS_UYU.shp', 'capa_edificaciones', true, NOW(), false),
                    ('05120100-0000-0000-0000-000000000103', '051201', 'PrediosNoFotografiados', 'uyuni-versionado-predios-no-fotografiados', 'PRE_NO_FOT.shp', 'capa_predios_no_fotografiados', true, NOW(), false),
                    ('05120100-0000-0000-0000-000000000104', '051201', 'Manzanas', 'uyuni-versionado-manzanas', 'MAN_SIS_UYU.shp', 'capa_manzanas', true, NOW(), false),
                    ('05120100-0000-0000-0000-000000000105', '051201', 'Distritos', 'uyuni-versionado-distritos', 'DIS_SIS_UYU.shp', 'capa_distritos', true, NOW(), false),
                    ('05120100-0000-0000-0000-000000000106', '051201', 'ZonasValuacion', 'uyuni-versionado-zonas', 'ZONA_SIS_UYU.shp', 'capa_zonas', true, NOW(), false),
                    ('05120100-0000-0000-0000-000000000107', '051201', 'Vias', 'uyuni-versionado-vias', 'VIA_INFO_UYU.shp', 'capa_vias', true, NOW(), false),
                    ('02200100-0000-0000-0000-000000000101', '022001', 'Manzanas', 'caranavi-versionado-manzanas', 'MANZANOS_PROY.shp', 'capa_manzanas', true, NOW(), false),
                    ('02200100-0000-0000-0000-000000000102', '022001', 'AreasUrbanas', 'caranavi-versionado-areas-urbanas', 'AREA_URBANA.shp', 'capa_areas_urbanas', true, NOW(), false),
                    ('02200100-0000-0000-0000-000000000103', '022001', 'PuntosGeodesicos', 'caranavi-versionado-puntos-geodesicos', 'puntos_geodesicos.shp', 'capa_puntos_geodesicos', true, NOW(), false)
                ON CONFLICT (id) DO NOTHING;
                """);

            migrationBuilder.CreateIndex(
                name: "uix_predios_municipio_triplete_activo",
                schema: "dominio",
                table: "predios",
                columns: new[] { "municipio_codigo", "cod_uv", "cod_man", "cod_pred" },
                unique: true,
                filter: "NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_capa_areas_urbanas_dataset_version_id",
                schema: "dominio",
                table: "capa_areas_urbanas",
                column: "dataset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_capa_areas_urbanas_geometria",
                schema: "dominio",
                table: "capa_areas_urbanas",
                column: "geometria",
                filter: "geometria IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_capa_puntos_geodesicos_dataset_version_id",
                schema: "dominio",
                table: "capa_puntos_geodesicos",
                column: "dataset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_capa_puntos_geodesicos_geometria",
                schema: "dominio",
                table: "capa_puntos_geodesicos",
                column: "geometria",
                filter: "geometria IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "uix_esquemas_capas_municipio_perfil",
                schema: "dominio",
                table: "esquemas_capas",
                columns: new[] { "municipio_codigo", "nombre_perfil" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uix_esquemas_capas_municipio_tipo",
                schema: "dominio",
                table: "esquemas_capas",
                columns: new[] { "municipio_codigo", "tipo_capa" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_dataset_versiones_municipios_municipio_codigo",
                schema: "dominio",
                table: "dataset_versiones",
                column: "municipio_codigo",
                principalSchema: "dominio",
                principalTable: "municipios",
                principalColumn: "codigo_ine",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_predios_municipios_municipio_codigo",
                schema: "dominio",
                table: "predios",
                column: "municipio_codigo",
                principalSchema: "dominio",
                principalTable: "municipios",
                principalColumn: "codigo_ine",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql("""
                CREATE TRIGGER trg_capa_areas_urbanas_inmutable_fila
                BEFORE UPDATE OR DELETE ON dominio.capa_areas_urbanas
                FOR EACH ROW EXECUTE FUNCTION dominio.fn_capa_versionada_inmutable_fila();
                CREATE TRIGGER trg_capa_areas_urbanas_truncate_prohibido
                BEFORE TRUNCATE ON dominio.capa_areas_urbanas
                FOR EACH STATEMENT EXECUTE FUNCTION dominio.fn_capa_versionada_truncate_prohibido();

                CREATE TRIGGER trg_capa_puntos_geodesicos_inmutable_fila
                BEFORE UPDATE OR DELETE ON dominio.capa_puntos_geodesicos
                FOR EACH ROW EXECUTE FUNCTION dominio.fn_capa_versionada_inmutable_fila();
                CREATE TRIGGER trg_capa_puntos_geodesicos_truncate_prohibido
                BEFORE TRUNCATE ON dominio.capa_puntos_geodesicos
                FOR EACH STATEMENT EXECUTE FUNCTION dominio.fn_capa_versionada_truncate_prohibido();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1 FROM dominio.dataset_versiones
                        WHERE municipio_codigo <> '051201'
                    ) OR EXISTS (
                        SELECT 1 FROM dominio.predios
                        WHERE municipio_codigo <> '051201'
                    ) THEN
                        RAISE EXCEPTION
                            'M015 Down bloqueado: existen datos asociados a municipios distintos de 051201.';
                    END IF;
                END;
                $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_dataset_versiones_municipios_municipio_codigo",
                schema: "dominio",
                table: "dataset_versiones");

            migrationBuilder.DropForeignKey(
                name: "fk_predios_municipios_municipio_codigo",
                schema: "dominio",
                table: "predios");

            migrationBuilder.Sql("""
                UPDATE dominio.dataset_versiones
                SET municipio_codigo = 'UYUNI'
                WHERE municipio_codigo = '051201';

                UPDATE dominio.predios
                SET municipio_codigo = 'UYUNI'
                WHERE municipio_codigo = '051201';
                """);

            migrationBuilder.DropTable(
                name: "capa_areas_urbanas",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "capa_puntos_geodesicos",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "esquemas_capas",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "municipios",
                schema: "dominio");

            migrationBuilder.DropIndex(
                name: "uix_predios_municipio_triplete_activo",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropColumn(
                name: "municipio_codigo",
                schema: "dominio",
                table: "predios");

            migrationBuilder.AlterColumn<string>(
                name: "municipio_codigo",
                schema: "dominio",
                table: "dataset_versiones",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(6)",
                oldMaxLength: 6);

            migrationBuilder.CreateIndex(
                name: "uix_predios_triplete_activo",
                schema: "dominio",
                table: "predios",
                columns: new[] { "cod_uv", "cod_man", "cod_pred" },
                unique: true,
                filter: "NOT is_deleted");
        }
    }
}
