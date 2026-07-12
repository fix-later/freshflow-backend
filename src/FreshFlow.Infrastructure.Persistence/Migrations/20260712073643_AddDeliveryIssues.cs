using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDeliveryIssues : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "delivery_issues",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                delivery_id = table.Column<Guid>(type: "uuid", nullable: false),
                issue_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                description = table.Column<string>(type: "text", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                reported_by = table.Column<Guid>(type: "uuid", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_delivery_issues", x => x.id);
                table.CheckConstraint("ck_delivery_issues_status", "status IN ('open','resolved')");
                table.CheckConstraint("ck_delivery_issues_type", "issue_type IN ('undeliverable','damaged','customer_rejected','other')");
                table.ForeignKey(
                    name: "fk_delivery_issues_delivery",
                    column: x => x.delivery_id,
                    principalTable: "deliveries",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_delivery_issues_delivery_id",
            table: "delivery_issues",
            column: "delivery_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "delivery_issues");
    }
}
