using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M014_IndicesEspacialesTilesMvt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_capa_zonas_geometria",
                schema: "dominio",
                table: "capa_zonas",
                column: "geometria",
                filter: "geometria IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_capa_vias_geometria",
                schema: "dominio",
                table: "capa_vias",
                column: "geometria",
                filter: "geometria IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_capa_predios_no_fotografiados_geometria",
                schema: "dominio",
                table: "capa_predios_no_fotografiados",
                column: "geometria",
                filter: "geometria IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_capa_parcelas_geometria",
                schema: "dominio",
                table: "capa_parcelas",
                column: "geometria",
                filter: "geometria IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_capa_manzanas_geometria",
                schema: "dominio",
                table: "capa_manzanas",
                column: "geometria",
                filter: "geometria IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_capa_edificaciones_geometria",
                schema: "dominio",
                table: "capa_edificaciones",
                column: "geometria",
                filter: "geometria IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ix_capa_distritos_geometria",
                schema: "dominio",
                table: "capa_distritos",
                column: "geometria",
                filter: "geometria IS NOT NULL")
                .Annotation("Npgsql:IndexMethod", "gist");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_capa_zonas_geometria",
                schema: "dominio",
                table: "capa_zonas");

            migrationBuilder.DropIndex(
                name: "ix_capa_vias_geometria",
                schema: "dominio",
                table: "capa_vias");

            migrationBuilder.DropIndex(
                name: "ix_capa_predios_no_fotografiados_geometria",
                schema: "dominio",
                table: "capa_predios_no_fotografiados");

            migrationBuilder.DropIndex(
                name: "ix_capa_parcelas_geometria",
                schema: "dominio",
                table: "capa_parcelas");

            migrationBuilder.DropIndex(
                name: "ix_capa_manzanas_geometria",
                schema: "dominio",
                table: "capa_manzanas");

            migrationBuilder.DropIndex(
                name: "ix_capa_edificaciones_geometria",
                schema: "dominio",
                table: "capa_edificaciones");

            migrationBuilder.DropIndex(
                name: "ix_capa_distritos_geometria",
                schema: "dominio",
                table: "capa_distritos");
        }
    }
}
