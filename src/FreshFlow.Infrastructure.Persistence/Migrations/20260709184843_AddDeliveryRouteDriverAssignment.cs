using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDeliveryRouteDriverAssignment : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "driver_user_id",
            table: "delivery_routes",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_delivery_routes_vehicle_service_date_assigned",
            table: "delivery_routes",
            columns: new[] { "vehicle_id", "service_date" },
            unique: true,
            filter: "status = 'assigned' AND deleted_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_delivery_routes_vehicle_service_date_assigned",
            table: "delivery_routes");

        migrationBuilder.DropColumn(
            name: "driver_user_id",
            table: "delivery_routes");
    }
}
