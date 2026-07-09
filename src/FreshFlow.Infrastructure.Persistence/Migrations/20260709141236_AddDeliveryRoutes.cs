using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDeliveryRoutes : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "delivery_routes",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                route_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                service_date = table.Column<DateOnly>(type: "date", nullable: false),
                route_metadata = table.Column<string>(type: "jsonb", nullable: false),
                total_distance_km = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                estimated_duration_minutes = table.Column<int>(type: "integer", nullable: true),
                estimated_cost = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                vehicle_id = table.Column<Guid>(type: "uuid", nullable: true),
                order_group_id = table.Column<Guid>(type: "uuid", nullable: true),
                created_by = table.Column<Guid>(type: "uuid", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_delivery_routes", x => x.id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_delivery_routes_created_by",
            table: "delivery_routes",
            column: "created_by");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_routes_order_group_id",
            table: "delivery_routes",
            column: "order_group_id");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_routes_service_date",
            table: "delivery_routes",
            column: "service_date");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_routes_status",
            table: "delivery_routes",
            column: "status");

        migrationBuilder.CreateIndex(
            name: "idx_delivery_routes_vehicle_service_date",
            table: "delivery_routes",
            columns: new[] { "vehicle_id", "service_date" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "delivery_routes");
    }
}
