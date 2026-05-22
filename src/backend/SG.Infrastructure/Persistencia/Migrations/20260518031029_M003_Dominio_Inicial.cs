using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M003_Dominio_Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dominio");

            migrationBuilder.CreateTable(
                name: "propietarios",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    apellidos = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cedula = table.Column<string>(type: "character varying(15)", maxLength: 15, nullable: true),
                    razon_social = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    nit = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    representante_legal = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    telefono = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    direccion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_propietarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usos_suelo",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    nombre = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    activo = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    orden = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usos_suelo", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "predios",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo_catastral = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    ubic_zona = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    ubic_manzana = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ubic_lote = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ubic_barrio = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ubic_direccion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ubic_referencia = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    superficie_declarada = table.Column<decimal>(type: "numeric(14,4)", nullable: false),
                    superficie_sig = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    superficie_oficial = table.Column<decimal>(type: "numeric(14,4)", nullable: true),
                    uso_suelo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    geometria = table.Column<Polygon>(type: "geometry(Polygon, 32719)", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_predios", x => x.id);
                    table.ForeignKey(
                        name: "fk_predios_usos_suelo_uso_suelo_id",
                        column: x => x.uso_suelo_id,
                        principalSchema: "dominio",
                        principalTable: "usos_suelo",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "documentos",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    predio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_archivo = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    minio_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    tipo_documento = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subido_por = table.Column<Guid>(type: "uuid", nullable: false),
                    subido_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    eliminado_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    eliminado_por = table.Column<Guid>(type: "uuid", nullable: true),
                    motivo_eliminacion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_documentos", x => x.id);
                    table.ForeignKey(
                        name: "fk_documentos_predios_predio_id",
                        column: x => x.predio_id,
                        principalSchema: "dominio",
                        principalTable: "predios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "historial_estados",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    predio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    estado_anterior = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    estado_nuevo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cambiado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    cambiado_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    observaciones = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historial_estados", x => x.id);
                    table.ForeignKey(
                        name: "fk_historial_estados_predios_predio_id",
                        column: x => x.predio_id,
                        principalSchema: "dominio",
                        principalTable: "predios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "relaciones_predio_propietario",
                schema: "dominio",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    predio_id = table.Column<Guid>(type: "uuid", nullable: false),
                    propietario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo_derecho = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    porcentaje = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    vigente_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    vigente_hasta = table.Column<DateOnly>(type: "date", nullable: true),
                    creado_por = table.Column<Guid>(type: "uuid", nullable: false),
                    creado_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_relaciones_predio_propietario", x => x.id);
                    table.ForeignKey(
                        name: "fk_relaciones_predio_propietario_predios_predio_id",
                        column: x => x.predio_id,
                        principalSchema: "dominio",
                        principalTable: "predios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_relaciones_predio_propietario_propietarios_propietario_id",
                        column: x => x.propietario_id,
                        principalSchema: "dominio",
                        principalTable: "propietarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_documentos_predio_id",
                schema: "dominio",
                table: "documentos",
                column: "predio_id");

            migrationBuilder.CreateIndex(
                name: "ix_historial_estados_predio_id",
                schema: "dominio",
                table: "historial_estados",
                column: "predio_id");

            migrationBuilder.CreateIndex(
                name: "ix_predios_uso_suelo_id",
                schema: "dominio",
                table: "predios",
                column: "uso_suelo_id");

            migrationBuilder.CreateIndex(
                name: "uix_predios_codigo_catastral",
                schema: "dominio",
                table: "predios",
                column: "codigo_catastral",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_propietarios_cedula",
                schema: "dominio",
                table: "propietarios",
                column: "cedula");

            migrationBuilder.CreateIndex(
                name: "ix_propietarios_nit",
                schema: "dominio",
                table: "propietarios",
                column: "nit");

            migrationBuilder.CreateIndex(
                name: "ix_relaciones_predio_propietario_predio_prop_desde",
                schema: "dominio",
                table: "relaciones_predio_propietario",
                columns: new[] { "predio_id", "propietario_id", "vigente_desde" });

            migrationBuilder.CreateIndex(
                name: "ix_relaciones_predio_propietario_propietario_id",
                schema: "dominio",
                table: "relaciones_predio_propietario",
                column: "propietario_id");

            migrationBuilder.CreateIndex(
                name: "uix_usos_suelo_codigo",
                schema: "dominio",
                table: "usos_suelo",
                column: "codigo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "documentos",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "historial_estados",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "relaciones_predio_propietario",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "predios",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "propietarios",
                schema: "dominio");

            migrationBuilder.DropTable(
                name: "usos_suelo",
                schema: "dominio");
        }
    }
}
