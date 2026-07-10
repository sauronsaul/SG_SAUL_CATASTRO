using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M010_ModeloVersionado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cod_man",
                schema: "dominio",
                table: "predios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cod_pred",
                schema: "dominio",
                table: "predios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "cod_uv",
                schema: "dominio",
                table: "predios",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "presente_en_version_activa",
                schema: "dominio",
                table: "predios",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ultima_version_vista_id",
                schema: "dominio",
                table: "predios",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "dataset_versiones",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero_version = table.Column<int>(type: "integer", nullable: false),
                    municipio_codigo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    importacion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    origen_descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    activado_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    activado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    archivado_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    archivado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dataset_versiones", x => x.id);
                    table.CheckConstraint("ck_dataset_versiones_numero_positivo", "numero_version > 0");
                    table.ForeignKey(
                        name: "fk_dataset_versiones_importaciones_importacion_id",
                        column: x => x.importacion_id,
                        principalSchema: "dominio",
                        principalTable: "importaciones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "capa_distritos",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geometria = table.Column<Polygon>(type: "geometry(Polygon,32719)", nullable: false),
                    codigo_geografico = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    cod_uv = table.Column<int>(type: "integer", nullable: true),
                    nombre = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    dataset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atributos_extra = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    fila_origen = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capa_distritos", x => x.id);
                    table.CheckConstraint("ck_capa_distritos_srid_32719", "ST_SRID(geometria) = 32719");
                    table.ForeignKey(
                        name: "fk_capa_distritos_dataset_versiones_dataset_version_id",
                        column: x => x.dataset_version_id,
                        principalSchema: "dominio",
                        principalTable: "dataset_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "capa_edificaciones",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geometria = table.Column<Polygon>(type: "geometry(Polygon,32719)", nullable: false),
                    id_edificacion_origen = table.Column<long>(type: "bigint", nullable: true),
                    codigo_geografico = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    cod_uv = table.Column<int>(type: "integer", nullable: true),
                    cod_man = table.Column<int>(type: "integer", nullable: true),
                    cod_pred = table.Column<int>(type: "integer", nullable: true),
                    numero_edificacion = table.Column<long>(type: "bigint", nullable: true),
                    piso = table.Column<long>(type: "bigint", nullable: true),
                    codigo_espacio = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    codigo_bloque = table.Column<long>(type: "bigint", nullable: true),
                    area_construida = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    dataset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atributos_extra = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    fila_origen = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capa_edificaciones", x => x.id);
                    table.CheckConstraint("ck_capa_edificaciones_srid_32719", "ST_SRID(geometria) = 32719");
                    table.ForeignKey(
                        name: "fk_capa_edificaciones_dataset_versiones_dataset_version_id",
                        column: x => x.dataset_version_id,
                        principalSchema: "dominio",
                        principalTable: "dataset_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "capa_manzanas",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geometria = table.Column<Polygon>(type: "geometry(Polygon,32719)", nullable: false),
                    codigo_geografico = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    cod_uv = table.Column<int>(type: "integer", nullable: true),
                    cod_man = table.Column<int>(type: "integer", nullable: true),
                    coordenada_origen = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                    dataset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atributos_extra = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    fila_origen = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capa_manzanas", x => x.id);
                    table.CheckConstraint("ck_capa_manzanas_srid_32719", "ST_SRID(geometria) = 32719");
                    table.ForeignKey(
                        name: "fk_capa_manzanas_dataset_versiones_dataset_version_id",
                        column: x => x.dataset_version_id,
                        principalSchema: "dominio",
                        principalTable: "dataset_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "capa_parcelas",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geometria = table.Column<Polygon>(type: "geometry(Polygon,32719)", nullable: false),
                    cod_uv = table.Column<int>(type: "integer", nullable: false),
                    cod_man = table.Column<int>(type: "integer", nullable: false),
                    cod_pred = table.Column<int>(type: "integer", nullable: false),
                    codigo_geografico = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    superficie = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    valuacion_zonal = table.Column<int>(type: "integer", nullable: true),
                    tipo_inmueble = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    servicio_alcantarillado = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    servicio_agua = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    servicio_luz = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    servicio_telefonia = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    nombre_propietario_origen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    nombre_via = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    direccion_barrio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    direccion_urbana = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    uso_terreno = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    topografia_terreno = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    dataset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atributos_extra = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    fila_origen = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capa_parcelas", x => x.id);
                    table.CheckConstraint("ck_capa_parcelas_srid_32719", "ST_SRID(geometria) = 32719");
                    table.ForeignKey(
                        name: "fk_capa_parcelas_dataset_versiones_dataset_version_id",
                        column: x => x.dataset_version_id,
                        principalSchema: "dominio",
                        principalTable: "dataset_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "capa_predios_no_fotografiados",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geometria = table.Column<Polygon>(type: "geometry(Polygon,32719)", nullable: false),
                    id_predio_origen = table.Column<long>(type: "bigint", nullable: true),
                    codigo_geografico = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    cod_uv = table.Column<int>(type: "integer", nullable: true),
                    cod_man = table.Column<int>(type: "integer", nullable: true),
                    cod_pred = table.Column<int>(type: "integer", nullable: true),
                    indicador_fotos = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                    foto_frente = table.Column<string>(type: "character varying(85)", maxLength: 85, nullable: true),
                    foto_derecha = table.Column<string>(type: "character varying(85)", maxLength: 85, nullable: true),
                    foto_izquierda = table.Column<string>(type: "character varying(85)", maxLength: 85, nullable: true),
                    dataset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atributos_extra = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    fila_origen = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capa_predios_no_fotografiados", x => x.id);
                    table.CheckConstraint("ck_capa_predios_no_fotografiados_srid_32719", "ST_SRID(geometria) = 32719");
                    table.ForeignKey(
                        name: "fk_capa_predios_no_fotografiados_dataset_versiones_dataset_ver",
                        column: x => x.dataset_version_id,
                        principalSchema: "dominio",
                        principalTable: "dataset_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "capa_vias",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geometria = table.Column<LineString>(type: "geometry(LineString,32719)", nullable: false),
                    material = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: true),
                    nombre = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    tipo = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    distancia_origen = table.Column<decimal>(type: "numeric(19,11)", nullable: true),
                    dataset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atributos_extra = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    fila_origen = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capa_vias", x => x.id);
                    table.CheckConstraint("ck_capa_vias_srid_32719", "ST_SRID(geometria) = 32719");
                    table.ForeignKey(
                        name: "fk_capa_vias_dataset_versiones_dataset_version_id",
                        column: x => x.dataset_version_id,
                        principalSchema: "dominio",
                        principalTable: "dataset_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "capa_zonas",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    geometria = table.Column<Polygon>(type: "geometry(Polygon,32719)", nullable: false),
                    nombre_zona = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: true),
                    id_zona_origen = table.Column<long>(type: "bigint", nullable: true),
                    codigo_geografico = table.Column<string>(type: "character varying(11)", maxLength: 11, nullable: true),
                    dataset_version_id = table.Column<Guid>(type: "uuid", nullable: false),
                    atributos_extra = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    fila_origen = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_capa_zonas", x => x.id);
                    table.CheckConstraint("ck_capa_zonas_srid_32719", "ST_SRID(geometria) = 32719");
                    table.ForeignKey(
                        name: "fk_capa_zonas_dataset_versiones_dataset_version_id",
                        column: x => x.dataset_version_id,
                        principalSchema: "dominio",
                        principalTable: "dataset_versiones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Backfill intencionalmente estricto: PostgreSQL debe abortar la migración
            // si cualquier ubicación legado no es numérica. No usar COALESCE/NULLIF.
            migrationBuilder.Sql(@"
UPDATE dominio.predios
SET cod_uv = ubic_zona::integer,
    cod_man = ubic_manzana::integer,
    cod_pred = ubic_lote::integer;
");

            migrationBuilder.AlterColumn<int>(
                name: "cod_uv",
                schema: "dominio",
                table: "predios",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "cod_man",
                schema: "dominio",
                table: "predios",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "cod_pred",
                schema: "dominio",
                table: "predios",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_predios_ultima_version_vista_id",
                schema: "dominio",
                table: "predios",
                column: "ultima_version_vista_id");

            migrationBuilder.CreateIndex(
                name: "uix_predios_triplete_activo",
                schema: "dominio",
                table: "predios",
                columns: new[] { "cod_uv", "cod_man", "cod_pred" },
                unique: true,
                filter: "NOT is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_capa_distritos_dataset_version_id",
                schema: "dominio",
                table: "capa_distritos",
                column: "dataset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_capa_edificaciones_dataset_version_id",
                schema: "dominio",
                table: "capa_edificaciones",
                column: "dataset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_capa_manzanas_dataset_version_id",
                schema: "dominio",
                table: "capa_manzanas",
                column: "dataset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_capa_parcelas_dataset_version_id",
                schema: "dominio",
                table: "capa_parcelas",
                column: "dataset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_capa_parcelas_version_triplete",
                schema: "dominio",
                table: "capa_parcelas",
                columns: new[] { "dataset_version_id", "cod_uv", "cod_man", "cod_pred" });

            migrationBuilder.CreateIndex(
                name: "ix_capa_predios_no_fotografiados_dataset_version_id",
                schema: "dominio",
                table: "capa_predios_no_fotografiados",
                column: "dataset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_capa_vias_dataset_version_id",
                schema: "dominio",
                table: "capa_vias",
                column: "dataset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_capa_zonas_dataset_version_id",
                schema: "dominio",
                table: "capa_zonas",
                column: "dataset_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_dataset_versiones_importacion_id",
                schema: "dominio",
                table: "dataset_versiones",
                column: "importacion_id");

            migrationBuilder.CreateIndex(
                name: "uix_dataset_versiones_municipio_activa",
                schema: "dominio",
                table: "dataset_versiones",
                column: "municipio_codigo",
                unique: true,
                filter: "estado = 'Activa'");

            migrationBuilder.CreateIndex(
                name: "uix_dataset_versiones_municipio_numero",
                schema: "dominio",
                table: "dataset_versiones",
                columns: new[] { "municipio_codigo", "numero_version" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_predios_dataset_versiones_ultima_version_vista_id",
                schema: "dominio",
                table: "predios",
                column: "ultima_version_vista_id",
                principalSchema: "dominio",
                principalTable: "dataset_versiones",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.Sql(@"
-- T3: INSERT no tiene trigger. La carga masiva solo escribe versiones EnCarga
-- por la máquina de estados del dominio; UPDATE/DELETE consulta por PK de versión.
CREATE OR REPLACE FUNCTION dominio.fn_capa_versionada_inmutable_fila()
RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE
    estado_version text;
BEGIN
    SELECT estado INTO estado_version
    FROM dominio.dataset_versiones
    WHERE id = OLD.dataset_version_id;

    IF NOT FOUND THEN
        RAISE EXCEPTION
            'Inmutabilidad de capa %: operación % sin DatasetVersion %.',
            TG_TABLE_NAME, TG_OP, OLD.dataset_version_id;
    END IF;

    IF TG_OP = 'UPDATE' AND estado_version <> 'EnCarga' THEN
        RAISE EXCEPTION
            'Inmutabilidad de capa %: operación % prohibida para DatasetVersion en estado %.',
            TG_TABLE_NAME, TG_OP, estado_version;
    END IF;

    IF TG_OP = 'DELETE' AND estado_version NOT IN ('EnCarga', 'Fallida', 'Descartada') THEN
        RAISE EXCEPTION
            'Inmutabilidad de capa %: operación % prohibida para DatasetVersion en estado %.',
            TG_TABLE_NAME, TG_OP, estado_version;
    END IF;

    IF TG_OP = 'UPDATE' THEN
        RETURN NEW;
    END IF;

    RETURN OLD;
END;
$$;

CREATE OR REPLACE FUNCTION dominio.fn_capa_versionada_truncate_prohibido()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION
        'Inmutabilidad de capa %: operación % prohibida; la purga usa DELETE sobre versiones Fallida o Descartada.',
        TG_TABLE_NAME, TG_OP;
END;
$$;

CREATE TRIGGER trg_capa_parcelas_inmutable_fila
BEFORE UPDATE OR DELETE ON dominio.capa_parcelas
FOR EACH ROW EXECUTE FUNCTION dominio.fn_capa_versionada_inmutable_fila();
CREATE TRIGGER trg_capa_parcelas_truncate_prohibido
BEFORE TRUNCATE ON dominio.capa_parcelas
FOR EACH STATEMENT EXECUTE FUNCTION dominio.fn_capa_versionada_truncate_prohibido();

CREATE TRIGGER trg_capa_edificaciones_inmutable_fila
BEFORE UPDATE OR DELETE ON dominio.capa_edificaciones
FOR EACH ROW EXECUTE FUNCTION dominio.fn_capa_versionada_inmutable_fila();
CREATE TRIGGER trg_capa_edificaciones_truncate_prohibido
BEFORE TRUNCATE ON dominio.capa_edificaciones
FOR EACH STATEMENT EXECUTE FUNCTION dominio.fn_capa_versionada_truncate_prohibido();

CREATE TRIGGER trg_capa_predios_no_fotografiados_inmutable_fila
BEFORE UPDATE OR DELETE ON dominio.capa_predios_no_fotografiados
FOR EACH ROW EXECUTE FUNCTION dominio.fn_capa_versionada_inmutable_fila();
CREATE TRIGGER trg_capa_predios_no_fotografiados_truncate_prohibido
BEFORE TRUNCATE ON dominio.capa_predios_no_fotografiados
FOR EACH STATEMENT EXECUTE FUNCTION dominio.fn_capa_versionada_truncate_prohibido();

CREATE TRIGGER trg_capa_manzanas_inmutable_fila
BEFORE UPDATE OR DELETE ON dominio.capa_manzanas
FOR EACH ROW EXECUTE FUNCTION dominio.fn_capa_versionada_inmutable_fila();
CREATE TRIGGER trg_capa_manzanas_truncate_prohibido
BEFORE TRUNCATE ON dominio.capa_manzanas
FOR EACH STATEMENT EXECUTE FUNCTION dominio.fn_capa_versionada_truncate_prohibido();

CREATE TRIGGER trg_capa_distritos_inmutable_fila
BEFORE UPDATE OR DELETE ON dominio.capa_distritos
FOR EACH ROW EXECUTE FUNCTION dominio.fn_capa_versionada_inmutable_fila();
CREATE TRIGGER trg_capa_distritos_truncate_prohibido
BEFORE TRUNCATE ON dominio.capa_distritos
FOR EACH STATEMENT EXECUTE FUNCTION dominio.fn_capa_versionada_truncate_prohibido();

CREATE TRIGGER trg_capa_zonas_inmutable_fila
BEFORE UPDATE OR DELETE ON dominio.capa_zonas
FOR EACH ROW EXECUTE FUNCTION dominio.fn_capa_versionada_inmutable_fila();
CREATE TRIGGER trg_capa_zonas_truncate_prohibido
BEFORE TRUNCATE ON dominio.capa_zonas
FOR EACH STATEMENT EXECUTE FUNCTION dominio.fn_capa_versionada_truncate_prohibido();

CREATE TRIGGER trg_capa_vias_inmutable_fila
BEFORE UPDATE OR DELETE ON dominio.capa_vias
FOR EACH ROW EXECUTE FUNCTION dominio.fn_capa_versionada_inmutable_fila();
CREATE TRIGGER trg_capa_vias_truncate_prohibido
BEFORE TRUNCATE ON dominio.capa_vias
FOR EACH STATEMENT EXECUTE FUNCTION dominio.fn_capa_versionada_truncate_prohibido();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_capa_parcelas_inmutable_fila ON dominio.capa_parcelas;
DROP TRIGGER IF EXISTS trg_capa_parcelas_truncate_prohibido ON dominio.capa_parcelas;
DROP TRIGGER IF EXISTS trg_capa_edificaciones_inmutable_fila ON dominio.capa_edificaciones;
DROP TRIGGER IF EXISTS trg_capa_edificaciones_truncate_prohibido ON dominio.capa_edificaciones;
DROP TRIGGER IF EXISTS trg_capa_predios_no_fotografiados_inmutable_fila ON dominio.capa_predios_no_fotografiados;
DROP TRIGGER IF EXISTS trg_capa_predios_no_fotografiados_truncate_prohibido ON dominio.capa_predios_no_fotografiados;
DROP TRIGGER IF EXISTS trg_capa_manzanas_inmutable_fila ON dominio.capa_manzanas;
DROP TRIGGER IF EXISTS trg_capa_manzanas_truncate_prohibido ON dominio.capa_manzanas;
DROP TRIGGER IF EXISTS trg_capa_distritos_inmutable_fila ON dominio.capa_distritos;
DROP TRIGGER IF EXISTS trg_capa_distritos_truncate_prohibido ON dominio.capa_distritos;
DROP TRIGGER IF EXISTS trg_capa_zonas_inmutable_fila ON dominio.capa_zonas;
DROP TRIGGER IF EXISTS trg_capa_zonas_truncate_prohibido ON dominio.capa_zonas;
DROP TRIGGER IF EXISTS trg_capa_vias_inmutable_fila ON dominio.capa_vias;
DROP TRIGGER IF EXISTS trg_capa_vias_truncate_prohibido ON dominio.capa_vias;
DROP FUNCTION IF EXISTS dominio.fn_capa_versionada_inmutable_fila();
DROP FUNCTION IF EXISTS dominio.fn_capa_versionada_truncate_prohibido();
");
            migrationBuilder.DropForeignKey(
                name: "fk_predios_dataset_versiones_ultima_version_vista_id",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropTable(
                name: "capa_distritos",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "capa_edificaciones",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "capa_manzanas",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "capa_parcelas",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "capa_predios_no_fotografiados",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "capa_vias",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "capa_zonas",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "dataset_versiones",
                schema: "dominio");

            migrationBuilder.DropIndex(
                name: "ix_predios_ultima_version_vista_id",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropIndex(
                name: "uix_predios_triplete_activo",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropColumn(
                name: "cod_man",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropColumn(
                name: "cod_pred",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropColumn(
                name: "cod_uv",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropColumn(
                name: "presente_en_version_activa",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropColumn(
                name: "ultima_version_vista_id",
                schema: "dominio",
                table: "predios");
        }
    }
}
