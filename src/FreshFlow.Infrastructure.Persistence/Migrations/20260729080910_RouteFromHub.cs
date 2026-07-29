using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class RouteFromHub : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "hub_id",
            table: "delivery_routes",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_delivery_routes_hub_id",
            table: "delivery_routes",
            column: "hub_id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_delivery_routes_hub_id",
            table: "delivery_routes");

        migrationBuilder.DropColumn(
            name: "hub_id",
            table: "delivery_routes");
    }
}
