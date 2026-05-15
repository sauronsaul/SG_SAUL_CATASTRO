using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M002_RefreshToken_RevokedReason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "revoked_reason",
                schema: "identidad",
                table: "refresh_tokens",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "revoked_reason",
                schema: "identidad",
                table: "refresh_tokens");
        }
    }
}
