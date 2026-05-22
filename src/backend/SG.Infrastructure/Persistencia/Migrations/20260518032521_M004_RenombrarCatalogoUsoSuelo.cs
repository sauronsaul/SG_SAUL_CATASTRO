using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M004_RenombrarCatalogoUsoSuelo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_predios_usos_suelo_uso_suelo_id",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropPrimaryKey(
                name: "pk_usos_suelo",
                schema: "dominio",
                table: "usos_suelo");

            migrationBuilder.RenameTable(
                name: "usos_suelo",
                schema: "dominio",
                newName: "catalogo_uso_suelo",
                newSchema: "dominio");

            migrationBuilder.AddPrimaryKey(
                name: "pk_catalogo_uso_suelo",
                schema: "dominio",
                table: "catalogo_uso_suelo",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_predios_catalogo_uso_suelo_uso_suelo_id",
                schema: "dominio",
                table: "predios",
                column: "uso_suelo_id",
                principalSchema: "dominio",
                principalTable: "catalogo_uso_suelo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_predios_catalogo_uso_suelo_uso_suelo_id",
                schema: "dominio",
                table: "predios");

            migrationBuilder.DropPrimaryKey(
                name: "pk_catalogo_uso_suelo",
                schema: "dominio",
                table: "catalogo_uso_suelo");

            migrationBuilder.RenameTable(
                name: "catalogo_uso_suelo",
                schema: "dominio",
                newName: "usos_suelo",
                newSchema: "dominio");

            migrationBuilder.AddPrimaryKey(
                name: "pk_usos_suelo",
                schema: "dominio",
                table: "usos_suelo",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_predios_usos_suelo_uso_suelo_id",
                schema: "dominio",
                table: "predios",
                column: "uso_suelo_id",
                principalSchema: "dominio",
                principalTable: "usos_suelo",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
