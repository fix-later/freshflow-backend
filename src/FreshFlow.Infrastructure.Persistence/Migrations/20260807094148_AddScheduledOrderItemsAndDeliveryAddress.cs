using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddScheduledOrderItemsAndDeliveryAddress : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "DeliveryAddressId",
            table: "scheduled_orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "scheduled_order_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ScheduledOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                MarketProductId = table.Column<Guid>(type: "uuid", nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_scheduled_order_items", x => x.Id);
                table.ForeignKey(
                    name: "fk_scheduled_order_items_scheduled_order",
                    column: x => x.ScheduledOrderId,
                    principalTable: "scheduled_orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_scheduled_order_items_market_product_id",
            table: "scheduled_order_items",
            column: "MarketProductId");

        migrationBuilder.CreateIndex(
            name: "idx_scheduled_order_items_scheduled_order_id",
            table: "scheduled_order_items",
            column: "ScheduledOrderId");

        // Backfill DeliveryAddressId from each restaurant's default delivery address where
        // one exists (SCRUM-386 §5.5). Schedules that end up with no address (no default set)
        // simply keep degrading to an empty Draft — same as before this migration.
        migrationBuilder.Sql("""
            UPDATE scheduled_orders so
            SET "DeliveryAddressId" = da."Id"
            FROM delivery_addresses da
            WHERE da."RestaurantId" = so."RestaurantId"
              AND da."IsDefault" = true
              AND da."DeletedAt" IS NULL
              AND so."DeliveryAddressId" IS NULL
              AND so.deleted_at IS NULL
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "scheduled_order_items");

        migrationBuilder.DropColumn(
            name: "DeliveryAddressId",
            table: "scheduled_orders");
    }
}
