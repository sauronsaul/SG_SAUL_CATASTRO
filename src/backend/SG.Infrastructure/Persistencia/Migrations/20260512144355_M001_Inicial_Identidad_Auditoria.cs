using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M001_Inicial_Identidad_Auditoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "auditoria");

            migrationBuilder.EnsureSchema(
                name: "identidad");

            migrationBuilder.EnsureSchema(
                name: "dominio");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:postgis", ",,");

            migrationBuilder.CreateTable(
                name: "auditoria",
                schema: "auditoria",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: true),
                    modulo = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    accion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entidad_tipo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    entidad_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    valor_anterior = table.Column<string>(type: "jsonb", nullable: true),
                    valor_nuevo = table.Column<string>(type: "jsonb", nullable: true),
                    resultado = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "OK"),
                    ip_origen = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    motivo = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_auditoria", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "identidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    descripcion = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    normalized_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "usuarios",
                schema: "identidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre_completo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    user_name = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    normalized_user_name = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    email_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    security_stamp = table.Column<string>(type: "text", nullable: true),
                    concurrency_stamp = table.Column<string>(type: "text", nullable: true),
                    phone_number = table.Column<string>(type: "text", nullable: true),
                    phone_number_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    two_factor_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    lockout_end = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lockout_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    access_failed_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuarios", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rol_claims",
                schema: "identidad",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rol_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_rol_claims_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identidad",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                schema: "identidad",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    usuario_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamptz", nullable: true),
                    replaced_by_token = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    created_by_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: false),
                    revoked_by_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_users_usuario_id",
                        column: x => x.usuario_id,
                        principalSchema: "identidad",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_claims",
                schema: "identidad",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claim_type = table.Column<string>(type: "text", nullable: true),
                    claim_value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_claims", x => x.id);
                    table.ForeignKey(
                        name: "fk_usuario_claims_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identidad",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_logins",
                schema: "identidad",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    provider_key = table.Column<string>(type: "text", nullable: false),
                    provider_display_name = table.Column<string>(type: "text", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey(
                        name: "fk_usuario_logins_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identidad",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_roles",
                schema: "identidad",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_usuario_roles_asp_net_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "identidad",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_usuario_roles_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identidad",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "usuario_tokens",
                schema: "identidad",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    login_provider = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_usuario_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey(
                        name: "fk_usuario_tokens_asp_net_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identidad",
                        principalTable: "usuarios",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_entidad_tipo_entidad_id",
                schema: "auditoria",
                table: "auditoria",
                columns: new[] { "entidad_tipo", "entidad_id" });

            migrationBuilder.CreateIndex(
                name: "ix_auditoria_timestamp",
                schema: "auditoria",
                table: "auditoria",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token",
                schema: "identidad",
                table: "refresh_tokens",
                column: "token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_usuario_id",
                schema: "identidad",
                table: "refresh_tokens",
                column: "usuario_id");

            migrationBuilder.CreateIndex(
                name: "ix_rol_claims_role_id",
                schema: "identidad",
                table: "rol_claims",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_roles_normalized_name",
                schema: "identidad",
                table: "roles",
                column: "normalized_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuario_claims_user_id",
                schema: "identidad",
                table: "usuario_claims",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_logins_user_id",
                schema: "identidad",
                table: "usuario_logins",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuario_roles_role_id",
                schema: "identidad",
                table: "usuario_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_normalized_email",
                schema: "identidad",
                table: "usuarios",
                column: "normalized_email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_usuarios_normalized_user_name",
                schema: "identidad",
                table: "usuarios",
                column: "normalized_user_name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "auditoria",
                schema: "auditoria");

            migrationBuilder.DropTable(
                name: "refresh_tokens",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "rol_claims",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "usuario_claims",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "usuario_logins",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "usuario_roles",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "usuario_tokens",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "identidad");

            migrationBuilder.DropTable(
                name: "usuarios",
                schema: "identidad");

            migrationBuilder.DropSchema(
                name: "dominio");
        }
    }
}
