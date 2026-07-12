using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M011_ImportacionAsincrona : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "error_carga",
                schema: "dominio",
                table: "dataset_versiones",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reporte_preliminar",
                schema: "dominio",
                table: "dataset_versiones",
                type: "jsonb",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "ruta_minio_paquete",
                schema: "dominio",
                table: "dataset_versiones",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "error_carga",
                schema: "dominio",
                table: "dataset_versiones");

            migrationBuilder.DropColumn(
                name: "reporte_preliminar",
                schema: "dominio",
                table: "dataset_versiones");

            migrationBuilder.DropColumn(
                name: "ruta_minio_paquete",
                schema: "dominio",
                table: "dataset_versiones");
        }
    }
}
