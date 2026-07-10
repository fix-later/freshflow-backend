using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddHubHandoverEvents : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "hub_handover_events",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                hub_id = table.Column<Guid>(type: "uuid", nullable: false),
                delivery_route_id = table.Column<Guid>(type: "uuid", nullable: false),
                driver_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                outbound_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false, defaultValue: "PENDING_CHECKOUT"),
                handed_over_by = table.Column<Guid>(type: "uuid", nullable: false),
                handed_over_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                driver_confirmed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hub_handover_events", x => x.id);
                table.CheckConstraint("ck_hub_handover_events_status", "status IN ('PENDING_CHECKOUT','CHECKED_OUT')");
                table.ForeignKey(
                    name: "fk_hub_handover_events_hub",
                    column: x => x.hub_id,
                    principalTable: "hubs",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_hub_handover_events_outbound_event",
                    column: x => x.outbound_event_id,
                    principalTable: "hub_outbound_events",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_hub_handover_events_created_at",
            table: "hub_handover_events",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "idx_hub_handover_events_delivery_route_id",
            table: "hub_handover_events",
            column: "delivery_route_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_handover_events_driver_user_id",
            table: "hub_handover_events",
            column: "driver_user_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_handover_events_hub_id",
            table: "hub_handover_events",
            column: "hub_id");

        migrationBuilder.CreateIndex(
            name: "idx_hub_handover_events_outbound_event_id",
            table: "hub_handover_events",
            column: "outbound_event_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "hub_handover_events");
    }
}
