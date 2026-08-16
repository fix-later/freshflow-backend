using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddHubDiscrepancies : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "hub_discrepancies",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                hub_id = table.Column<Guid>(type: "uuid", nullable: false),
                inbound_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                affected_quantity = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                condition_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "OPEN"),
                acknowledged_by = table.Column<Guid>(type: "uuid", nullable: true),
                acknowledged_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hub_discrepancies", x => x.id);
                table.CheckConstraint("ck_hub_discrepancies_affected_quantity_positive", "affected_quantity > 0");
                table.CheckConstraint("ck_hub_discrepancies_condition_status", "condition_status IN ('MISSING', 'DAMAGED', 'PARTIAL')");
                table.CheckConstraint("ck_hub_discrepancies_status", "status IN ('OPEN', 'ACKNOWLEDGED')");
                table.ForeignKey(
                    name: "fk_hub_discrepancies_hub",
                    column: x => x.hub_id,
                    principalTable: "hubs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_hub_discrepancies_inbound_event",
                    column: x => x.inbound_event_id,
                    principalTable: "hub_inbound_events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_hub_discrepancies_created_at",
            table: "hub_discrepancies",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "idx_hub_discrepancies_hub_id",
            table: "hub_discrepancies",
            column: "hub_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_discrepancies_inbound_event_id",
            table: "hub_discrepancies",
            column: "inbound_event_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_discrepancies_order_id",
            table: "hub_discrepancies",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_discrepancies_order_item_id",
            table: "hub_discrepancies",
            column: "order_item_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_discrepancies_status",
            table: "hub_discrepancies",
            column: "status");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "hub_discrepancies");
    }
}
