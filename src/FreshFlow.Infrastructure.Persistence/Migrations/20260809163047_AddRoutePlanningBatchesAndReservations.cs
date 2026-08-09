using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRoutePlanningBatchesAndReservations : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_delivery_routes_vehicle_service_date_assigned",
            table: "delivery_routes");

        migrationBuilder.AddColumn<DateTime>(
            name: "estimated_return_at",
            table: "delivery_routes",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "planned_load_kg",
            table: "delivery_routes",
            type: "numeric(14,3)",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "route_plan_id",
            table: "delivery_routes",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "routing_profile",
            table: "delivery_routes",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "suggested_vehicle_id",
            table: "delivery_routes",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "route_plans",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                hub_id = table.Column<Guid>(type: "uuid", nullable: false),
                service_date = table.Column<DateOnly>(type: "date", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                optimization_criteria = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                routing_provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                is_estimated = table.Column<bool>(type: "boolean", nullable: false),
                input_revision = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                unassigned_json = table.Column<string>(type: "jsonb", nullable: false),
                vehicles_used = table.Column<int>(type: "integer", nullable: false),
                total_load_kg = table.Column<decimal>(type: "numeric(14,3)", nullable: false),
                total_distance_km = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                estimated_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                estimated_cost = table.Column<decimal>(type: "numeric(16,2)", nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_route_plans", x => x.id);
                table.CheckConstraint("ck_route_plans_status", "status IN ('proposed','approved','stale','superseded')");
            });

        migrationBuilder.CreateIndex(
            name: "idx_delivery_routes_route_plan_id",
            table: "delivery_routes",
            column: "route_plan_id");

        migrationBuilder.CreateIndex(
            name: "ux_delivery_routes_vehicle_service_date_reserved",
            table: "delivery_routes",
            columns: new[] { "vehicle_id", "service_date" },
            unique: true,
            filter: "vehicle_id IS NOT NULL AND status <> 'cancelled' AND deleted_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "idx_route_plans_input_revision",
            table: "route_plans",
            column: "input_revision");

        migrationBuilder.CreateIndex(
            name: "ux_route_plans_hub_date_proposed",
            table: "route_plans",
            columns: new[] { "hub_id", "service_date" },
            unique: true,
            filter: "status = 'proposed' AND deleted_at IS NULL");

        migrationBuilder.AddForeignKey(
            name: "fk_delivery_routes_route_plan",
            table: "delivery_routes",
            column: "route_plan_id",
            principalTable: "route_plans",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_delivery_routes_route_plan",
            table: "delivery_routes");

        migrationBuilder.DropTable(
            name: "route_plans");

        migrationBuilder.DropIndex(
            name: "idx_delivery_routes_route_plan_id",
            table: "delivery_routes");

        migrationBuilder.DropIndex(
            name: "ux_delivery_routes_vehicle_service_date_reserved",
            table: "delivery_routes");

        migrationBuilder.DropColumn(
            name: "estimated_return_at",
            table: "delivery_routes");

        migrationBuilder.DropColumn(
            name: "planned_load_kg",
            table: "delivery_routes");

        migrationBuilder.DropColumn(
            name: "route_plan_id",
            table: "delivery_routes");

        migrationBuilder.DropColumn(
            name: "routing_profile",
            table: "delivery_routes");

        migrationBuilder.DropColumn(
            name: "suggested_vehicle_id",
            table: "delivery_routes");

        migrationBuilder.CreateIndex(
            name: "ux_delivery_routes_vehicle_service_date_assigned",
            table: "delivery_routes",
            columns: new[] { "vehicle_id", "service_date" },
            unique: true,
            filter: "status = 'assigned' AND deleted_at IS NULL");
    }
}
