using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddVehicleHubId : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "hub_id",
            table: "vehicles",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_vehicles_hub_id",
            table: "vehicles",
            column: "hub_id",
            filter: "deleted_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_vehicles_hub_id",
            table: "vehicles");

        migrationBuilder.DropColumn(
            name: "hub_id",
            table: "vehicles");
    }
}
