using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddCrossDockAndOutbound : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "cross_dock_transfers",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                hub_id = table.Column<Guid>(type: "uuid", nullable: false),
                inbound_event_id = table.Column<Guid>(type: "uuid", nullable: false),
                outbound_route_id = table.Column<Guid>(type: "uuid", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_cross_dock_transfers", x => x.id);
                table.CheckConstraint("ck_cross_dock_transfers_status", "status IN ('pending', 'in_progress', 'completed')");
                table.ForeignKey(
                    name: "fk_cross_dock_transfers_hub",
                    column: x => x.hub_id,
                    principalTable: "hubs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_cross_dock_transfers_inbound_event",
                    column: x => x.inbound_event_id,
                    principalTable: "hub_inbound_events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "hub_outbound_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                hub_id = table.Column<Guid>(type: "uuid", nullable: false),
                destination_route_id = table.Column<Guid>(type: "uuid", nullable: false),
                items = table.Column<string>(type: "jsonb", nullable: false),
                total_quantity_kg = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                dispatched_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                recorded_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hub_outbound_events", x => x.id);
                table.CheckConstraint("ck_hub_outbound_events_total_quantity_kg_positive", "total_quantity_kg > 0");
                table.ForeignKey(
                    name: "fk_hub_outbound_events_hub",
                    column: x => x.hub_id,
                    principalTable: "hubs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_cross_dock_transfers_created_at",
            table: "cross_dock_transfers",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "idx_cross_dock_transfers_hub_id",
            table: "cross_dock_transfers",
            column: "hub_id");

        migrationBuilder.CreateIndex(
            name: "idx_cross_dock_transfers_inbound_event_id",
            table: "cross_dock_transfers",
            column: "inbound_event_id");

        migrationBuilder.CreateIndex(
            name: "idx_cross_dock_transfers_outbound_route_id",
            table: "cross_dock_transfers",
            column: "outbound_route_id");

        migrationBuilder.CreateIndex(
            name: "idx_cross_dock_transfers_status",
            table: "cross_dock_transfers",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "idx_hub_outbound_events_created_at",
            table: "hub_outbound_events",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "idx_hub_outbound_events_destination_route_id",
            table: "hub_outbound_events",
            column: "destination_route_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_outbound_events_dispatched_at",
            table: "hub_outbound_events",
            column: "dispatched_at");

        migrationBuilder.CreateIndex(
            name: "idx_hub_outbound_events_hub_id",
            table: "hub_outbound_events",
            column: "hub_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "cross_dock_transfers");

        migrationBuilder.DropTable(
            name: "hub_outbound_events");
    }
}
