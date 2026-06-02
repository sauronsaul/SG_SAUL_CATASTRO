using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M006_Sprint3_Importacion_Construccion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "uso_suelo_id",
                schema: "dominio",
                table: "predios",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "codigo_origen",
                schema: "dominio",
                table: "predios",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "detalle_revision",
                schema: "dominio",
                table: "predios",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "propietario_referencia",
                schema: "dominio",
                table: "predios",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "requiere_revision",
                schema: "dominio",
                table: "predios",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "tipo_inmueble_origen",
                schema: "dominio",
                table: "predios",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "construcciones",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    predio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    pisos = table.Column<int>(type: "integer", nullable: false),
                    bloque = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    area_construida = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    tipo_construccion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_construcciones", x => x.id);
                    table.ForeignKey(
                        name: "fk_construcciones_predios_predio_id",
                        column: x => x.predio_id,
                        principalSchema: "dominio",
                        principalTable: "predios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "importaciones",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ruta_minio_zip = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    fecha_importacion = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    importado_por_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_filas = table.Column<int>(type: "integer", nullable: false),
                    filas_importadas = table.Column<int>(type: "integer", nullable: false),
                    filas_con_advertencia = table.Column<int>(type: "integer", nullable: false),
                    filas_rechazadas = table.Column<int>(type: "integer", nullable: false),
                    filas_omitidas = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_importaciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "perfiles_importacion",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tipo_capa = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    row_version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_perfiles_importacion", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mapeos_columna",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    perfil_importacion_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_columna_origen = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    campo_destino = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    es_obligatorio = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mapeos_columna", x => x.id);
                    table.ForeignKey(
                        name: "fk_mapeos_columna_perfiles_importacion_perfil_importacion_id",
                        column: x => x.perfil_importacion_id,
                        principalSchema: "dominio",
                        principalTable: "perfiles_importacion",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "equivalencias_valor",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapeo_columna_id = table.Column<Guid>(type: "uuid", nullable: false),
                    valor_origen = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valor_destino = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_equivalencias_valor", x => x.id);
                    table.ForeignKey(
                        name: "fk_equivalencias_valor_mapeos_columna_mapeo_columna_id",
                        column: x => x.mapeo_columna_id,
                        principalSchema: "dominio",
                        principalTable: "mapeos_columna",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_construcciones_predio_id",
                schema: "dominio",
                table: "construcciones",
                column: "predio_id");

            migrationBuilder.CreateIndex(
                name: "uix_equivalencias_valor_mapeo_origen",
                schema: "dominio",
                table: "equivalencias_valor",
                columns: new[] { "mapeo_columna_id", "valor_origen" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_importaciones_fecha",
                schema: "dominio",
                table: "importaciones",
                column: "fecha_importacion");

            migrationBuilder.CreateIndex(
                name: "ix_importaciones_perfil_id",
                schema: "dominio",
                table: "importaciones",
                column: "perfil_id");

            migrationBuilder.CreateIndex(
                name: "uix_mapeos_columna_perfil_origen",
                schema: "dominio",
                table: "mapeos_columna",
                columns: new[] { "perfil_importacion_id", "nombre_columna_origen" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "uix_perfiles_importacion_nombre",
                schema: "dominio",
                table: "perfiles_importacion",
                column: "nombre",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "construcciones",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "equivalencias_valor",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "importaciones",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "mapeos_columna",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "perfiles_importacion",
                schema: "dominio");

            migrationBuilder.DropColumn(
                name: "codigo_origen",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropColumn(
                name: "detalle_revision",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropColumn(
                name: "propietario_referencia",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropColumn(
                name: "requiere_revision",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropColumn(
                name: "tipo_inmueble_origen",
                schema: "dominio",
                table: "predios");

            migrationBuilder.AlterColumn<Guid>(
                name: "uso_suelo_id",
                schema: "dominio",
                table: "predios",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
