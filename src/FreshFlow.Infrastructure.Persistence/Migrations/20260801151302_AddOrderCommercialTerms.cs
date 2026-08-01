using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderCommercialTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MinimumOrderQuantity",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_distance_km",
                table: "orders",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_fee",
                table: "orders",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "subtotal_amount",
                table: "orders",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_amount",
                table: "orders",
                type: "numeric(14,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "locked_vat_amount",
                table: "order_items",
                type: "numeric(14,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "vat_rate_code",
                table: "order_items",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "vat_rate_percent",
                table: "order_items",
                type: "numeric(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_fee_per_km",
                table: "operational_settings",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 5000m);

            migrationBuilder.Sql(
                """
                UPDATE orders
                SET subtotal_amount = "TotalAmount"
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MinimumOrderQuantity",
                table: "products");

            migrationBuilder.DropColumn(
                name: "delivery_distance_km",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_fee",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "subtotal_amount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "vat_amount",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "locked_vat_amount",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "vat_rate_code",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "vat_rate_percent",
                table: "order_items");

            migrationBuilder.DropColumn(
                name: "delivery_fee_per_km",
                table: "operational_settings");
        }
    }
}
