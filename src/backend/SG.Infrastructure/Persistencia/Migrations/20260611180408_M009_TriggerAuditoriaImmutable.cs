using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SG.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class M009_TriggerAuditoriaImmutable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE OR REPLACE FUNCTION auditoria.fn_auditoria_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION
        'Violación append-only: operación % sobre auditoria.auditoria prohibida (ADR 0044).',
        TG_OP;
END;
$$;

CREATE TRIGGER trg_auditoria_immutable
BEFORE UPDATE OR DELETE OR TRUNCATE ON auditoria.auditoria
FOR EACH STATEMENT EXECUTE FUNCTION auditoria.fn_auditoria_immutable();
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TRIGGER IF EXISTS trg_auditoria_immutable ON auditoria.auditoria;
DROP FUNCTION IF EXISTS auditoria.fn_auditoria_immutable();
");
        }
    }
}
