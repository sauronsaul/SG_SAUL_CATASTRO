using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M007_NombreArchivoShpPerfilImportacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Paso 1: columna NOT NULL con DEFAULT '' como andamio para las filas existentes.
            migrationBuilder.AddColumn<string>(
                name: "nombre_archivo_shp",
                schema: "dominio",
                table: "perfiles_importacion",
                type: "character varying(260)",
                maxLength: 260,
                nullable: false,
                defaultValue: "");

            // Paso 2: rellenar los perfiles existentes de Uyuni con el nombre exacto del .shp.
            migrationBuilder.Sql(
                "UPDATE dominio.perfiles_importacion " +
                "SET nombre_archivo_shp = 'pre_uyu_sis.shp' " +
                "WHERE nombre = 'uyuni-predios';");

            migrationBuilder.Sql(
                "UPDATE dominio.perfiles_importacion " +
                "SET nombre_archivo_shp = 'edi_uyu_sis.shp' " +
                "WHERE nombre = 'uyuni-construcciones';");

            // Verificación: abortar si alguna fila quedó con nombre vacío.
            // Protege contra renombres de perfil que no coincidan con los WHERE anteriores.
            migrationBuilder.Sql("""
                DO $$
                DECLARE
                    vacias INTEGER;
                BEGIN
                    SELECT COUNT(*) INTO vacias
                    FROM dominio.perfiles_importacion
                    WHERE nombre_archivo_shp = '';

                    IF vacias > 0 THEN
                        RAISE EXCEPTION
                            'M007: % perfil(s) con nombre_archivo_shp vacío — los UPDATE no cubrieron todos los perfiles existentes. Migración abortada.',
                            vacias;
                    END IF;
                END;
                $$;
                """);

            // Paso 3: retirar el andamio; la columna queda NOT NULL sin DEFAULT (fail-fast).
            migrationBuilder.Sql(
                "ALTER TABLE dominio.perfiles_importacion " +
                "ALTER COLUMN nombre_archivo_shp DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "nombre_archivo_shp",
                schema: "dominio",
                table: "perfiles_importacion");
        }
    }
}
