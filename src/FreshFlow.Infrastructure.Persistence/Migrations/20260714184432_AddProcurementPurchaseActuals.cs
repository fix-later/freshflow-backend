using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProcurementPurchaseActuals : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "actual_quantity",
            table: "procurement_batch_items",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "actual_unit_price",
            table: "procurement_batch_items",
            type: "numeric(12,2)",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "purchased_at",
            table: "procurement_batch_items",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "actual_quantity",
            table: "procurement_batch_items");

        migrationBuilder.DropColumn(
            name: "actual_unit_price",
            table: "procurement_batch_items");

        migrationBuilder.DropColumn(
            name: "purchased_at",
            table: "procurement_batch_items");
    }
}
