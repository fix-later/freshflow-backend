using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class HubSortingProgressKeyByHubDate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_hub_sorting_progress_route_item_active",
            table: "hub_sorting_progress");

        migrationBuilder.AlterColumn<Guid>(
            name: "route_id",
            table: "hub_sorting_progress",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid");

        migrationBuilder.AddColumn<Guid>(
            name: "hub_id",
            table: "hub_sorting_progress",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

        migrationBuilder.AddColumn<DateOnly>(
            name: "service_date",
            table: "hub_sorting_progress",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1, 1, 1));

        migrationBuilder.CreateIndex(
            name: "ux_hub_sorting_progress_hub_date_item_active",
            table: "hub_sorting_progress",
            columns: new[] { "hub_id", "service_date", "order_item_id" },
            unique: true,
            filter: "deleted_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_hub_sorting_progress_hub_date_item_active",
            table: "hub_sorting_progress");

        migrationBuilder.DropColumn(
            name: "hub_id",
            table: "hub_sorting_progress");

        migrationBuilder.DropColumn(
            name: "service_date",
            table: "hub_sorting_progress");

        migrationBuilder.AlterColumn<Guid>(
            name: "route_id",
            table: "hub_sorting_progress",
            type: "uuid",
            nullable: false,
            defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "ux_hub_sorting_progress_route_item_active",
            table: "hub_sorting_progress",
            columns: new[] { "route_id", "order_item_id" },
            unique: true,
            filter: "deleted_at IS NULL");
    }
}
