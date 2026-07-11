using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddHubInboundAndInventory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "hub_inbound_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                hub_id = table.Column<Guid>(type: "uuid", nullable: false),
                source_market_id = table.Column<Guid>(type: "uuid", nullable: true),
                delivery_route_id = table.Column<Guid>(type: "uuid", nullable: true),
                delivery_schedule_id = table.Column<Guid>(type: "uuid", nullable: true),
                items = table.Column<string>(type: "jsonb", nullable: false),
                total_quantity_kg = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                arrived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                hub_staff_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "PENDING"),
                condition_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "OK"),
                discrepancy_notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hub_inbound_events", x => x.id);
                table.CheckConstraint("ck_hub_inbound_events_status", "status IN ('PENDING', 'ARRIVED_AT_HUB')");
                table.CheckConstraint("ck_hub_inbound_events_total_quantity_kg_positive", "total_quantity_kg > 0");
                table.ForeignKey(
                    name: "fk_hub_inbound_events_hub",
                    column: x => x.hub_id,
                    principalTable: "hubs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hub_inventory",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                hub_id = table.Column<Guid>(type: "uuid", nullable: false),
                market_product_id = table.Column<Guid>(type: "uuid", nullable: false),
                quantity_in = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m),
                quantity_out = table.Column<decimal>(type: "numeric(12,2)", nullable: false, defaultValue: 0m),
                quantity_available = table.Column<decimal>(type: "numeric(12,2)", nullable: false, computedColumnSql: "quantity_in - quantity_out", stored: true),
                recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hub_inventory", x => x.id);
                table.CheckConstraint("ck_hub_inventory_quantity_available_non_negative", "quantity_in >= quantity_out");
                table.CheckConstraint("ck_hub_inventory_quantity_in_non_negative", "quantity_in >= 0");
                table.CheckConstraint("ck_hub_inventory_quantity_out_non_negative", "quantity_out >= 0");
                table.ForeignKey(
                    name: "fk_hub_inventory_hub",
                    column: x => x.hub_id,
                    principalTable: "hubs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_hub_inbound_events_arrived_at",
            table: "hub_inbound_events",
            column: "arrived_at");

        migrationBuilder.CreateIndex(
            name: "idx_hub_inbound_events_delivery_schedule_id",
            table: "hub_inbound_events",
            column: "delivery_schedule_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_inbound_events_hub_id",
            table: "hub_inbound_events",
            column: "hub_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_inbound_events_status",
            table: "hub_inbound_events",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "ux_hub_inbound_events_hub_delivery_schedule_active",
            table: "hub_inbound_events",
            columns: new[] { "hub_id", "delivery_schedule_id" },
            unique: true,
            filter: "delivery_schedule_id IS NOT NULL AND deleted_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "idx_hub_inventory_hub_id",
            table: "hub_inventory",
            column: "hub_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_inventory_market_product_id",
            table: "hub_inventory",
            column: "market_product_id");

        migrationBuilder.CreateIndex(
            name: "ux_hub_inventory_hub_market_product",
            table: "hub_inventory",
            columns: new[] { "hub_id", "market_product_id" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "hub_inbound_events");

        migrationBuilder.DropTable(
            name: "hub_inventory");
    }
}
