using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMarketHubProcurementPlan : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "market_id",
            table: "hubs",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "idx_procurement_batches_hub_date",
            table: "procurement_batches",
            columns: new[] { "hub_id", "batch_date" },
            filter: "\"deleted_at\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "ux_hubs_active_market",
            table: "hubs",
            column: "market_id",
            unique: true,
            filter: "\"deleted_at\" IS NULL AND \"is_active\" = TRUE AND \"market_id\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_procurement_batches_hub_date",
            table: "procurement_batches");

        migrationBuilder.DropIndex(
            name: "ux_hubs_active_market",
            table: "hubs");

        migrationBuilder.DropColumn(
            name: "market_id",
            table: "hubs");
    }
}
