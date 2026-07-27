using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddHubSortingProgress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "hub_sorting_progress",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                route_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                sorted_quantity_kg = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "PENDING"),
                sorted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                sorted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_hub_sorting_progress", x => x.id);
                table.CheckConstraint("ck_hub_sorting_progress_qty_nonnegative", "sorted_quantity_kg >= 0");
                table.CheckConstraint("ck_hub_sorting_progress_status", "status IN ('PENDING', 'SORTED')");
            });

        migrationBuilder.CreateIndex(
            name: "ux_hub_sorting_progress_route_item_active",
            table: "hub_sorting_progress",
            columns: new[] { "route_id", "order_item_id" },
            unique: true,
            filter: "deleted_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "hub_sorting_progress");
    }
}
