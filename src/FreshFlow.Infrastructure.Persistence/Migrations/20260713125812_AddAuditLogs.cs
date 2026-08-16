using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddAuditLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "audit_logs",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                details = table.Column<string>(type: "jsonb", nullable: true),
                occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_audit_logs", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_action",
            table: "audit_logs",
            column: "action");

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_actor_id",
            table: "audit_logs",
            column: "actor_id");

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_created_at",
            table: "audit_logs",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "IX_audit_logs_entity_type",
            table: "audit_logs",
            column: "entity_type");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "audit_logs");
    }
}
