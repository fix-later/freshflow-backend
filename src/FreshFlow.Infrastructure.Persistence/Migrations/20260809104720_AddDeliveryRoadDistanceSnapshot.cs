using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddDeliveryRoadDistanceSnapshot : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "delivery_distance_meters",
            table: "orders",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "delivery_duration_seconds",
            table: "orders",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "delivery_fee_calculated_at",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "delivery_origin_latitude",
            table: "orders",
            type: "numeric(9,6)",
            precision: 9,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "delivery_origin_longitude",
            table: "orders",
            type: "numeric(9,6)",
            precision: 9,
            scale: 6,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "routing_provider",
            table: "orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "base_fee",
            table: "operational_settings",
            type: "numeric(14,2)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "minimum_fee",
            table: "operational_settings",
            type: "numeric(14,2)",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "rounding_unit",
            table: "operational_settings",
            type: "numeric(14,2)",
            nullable: false,
            defaultValue: 0m);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "delivery_distance_meters",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "delivery_duration_seconds",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "delivery_fee_calculated_at",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "delivery_origin_latitude",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "delivery_origin_longitude",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "routing_provider",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "base_fee",
            table: "operational_settings");

        migrationBuilder.DropColumn(
            name: "minimum_fee",
            table: "operational_settings");

        migrationBuilder.DropColumn(
            name: "rounding_unit",
            table: "operational_settings");
    }
}
