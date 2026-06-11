using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M008_SepararContadoresPreviewConfirmacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Nuevas columnas de preview ──────────────────────────────────
            migrationBuilder.AddColumn<int>(
                name: "filas_estimadas_a_crear",
                schema: "dominio",
                table: "importaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "filas_estimadas_a_actualizar",
                schema: "dominio",
                table: "importaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "filas_estimadas_a_omitir",
                schema: "dominio",
                table: "importaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "filas_estimadas_rechazadas",
                schema: "dominio",
                table: "importaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "filas_estimadas_con_advertencia",
                schema: "dominio",
                table: "importaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // ── Nuevas columnas de confirmación ─────────────────────────────
            // filas_omitidas, filas_rechazadas, filas_con_advertencia ya
            // existían con el mismo nombre — no se tocan.

            migrationBuilder.AddColumn<int>(
                name: "filas_creadas",
                schema: "dominio",
                table: "importaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "filas_actualizadas",
                schema: "dominio",
                table: "importaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // ── Backfill ─────────────────────────────────────────────────────
            // Estado Confirmada: el modelo anterior acumulaba creates+updates en
            // filas_importadas. No es posible reconstruir el split; se asigna el
            // total a filas_creadas como mejor aproximación.
            migrationBuilder.Sql(@"
UPDATE dominio.importaciones
SET filas_creadas = filas_importadas
WHERE estado = 'Confirmada';
");

            // Estado PreviewGenerado y Fallida: la única escritura fue el preview.
            // Las columnas filas_omitidas/rechazadas/con_advertencia cambian de
            // semántica en esta migración (ahora significan resultado de confirmación),
            // por lo que los valores de preview DEBEN moverse a las columnas estimadas
            // y las de confirmación quedar en 0. filas_importadas contenía
            // crear+actualizar del preview → filas_estimadas_a_crear (misma
            // aproximación aceptada para Confirmadas; ver ADR 0037).
            migrationBuilder.Sql(@"
UPDATE dominio.importaciones
SET filas_estimadas_a_crear         = filas_importadas,
    filas_estimadas_a_omitir        = filas_omitidas,
    filas_estimadas_rechazadas      = filas_rechazadas,
    filas_estimadas_con_advertencia = filas_con_advertencia,
    filas_omitidas        = 0,
    filas_rechazadas      = 0,
    filas_con_advertencia = 0
WHERE estado IN ('PreviewGenerado', 'Fallida');
");

            // ── Eliminar columna obsoleta ────────────────────────────────────
            migrationBuilder.DropColumn(
                name: "filas_importadas",
                schema: "dominio",
                table: "importaciones");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "filas_importadas",
                schema: "dominio",
                table: "importaciones",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill inverso: reconstruir filas_importadas = creadas + actualizadas
            migrationBuilder.Sql(@"
UPDATE dominio.importaciones
SET filas_importadas = filas_creadas + filas_actualizadas
WHERE estado = 'Confirmada';
");

            migrationBuilder.DropColumn(
                name: "filas_estimadas_a_crear",
                schema: "dominio",
                table: "importaciones");

            migrationBuilder.DropColumn(
                name: "filas_estimadas_a_actualizar",
                schema: "dominio",
                table: "importaciones");

            migrationBuilder.DropColumn(
                name: "filas_estimadas_a_omitir",
                schema: "dominio",
                table: "importaciones");

            migrationBuilder.DropColumn(
                name: "filas_estimadas_rechazadas",
                schema: "dominio",
                table: "importaciones");

            migrationBuilder.DropColumn(
                name: "filas_estimadas_con_advertencia",
                schema: "dominio",
                table: "importaciones");

            migrationBuilder.DropColumn(
                name: "filas_creadas",
                schema: "dominio",
                table: "importaciones");

            migrationBuilder.DropColumn(
                name: "filas_actualizadas",
                schema: "dominio",
                table: "importaciones");
        }
    }
}
