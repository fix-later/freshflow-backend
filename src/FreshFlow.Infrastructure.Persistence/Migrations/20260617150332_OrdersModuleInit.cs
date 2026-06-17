using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class OrdersModuleInit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "orders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                OrderGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                ScheduledOrderId = table.Column<Guid>(type: "uuid", nullable: true),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                PaymentStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ScheduledFor = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TotalAmount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                Notes = table.Column<string>(type: "text", nullable: true),
                CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CancellationReason = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_orders", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "scheduled_orders",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                RecurrenceType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                FirstRunAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                LastExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Notes = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_scheduled_orders", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "order_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                MarketProductId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductNameSnapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                UnitPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                LockedUnitPrice = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                LockedTotal = table.Column<decimal>(type: "numeric(14,2)", nullable: true),
                ActualQuantity = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_items", x => x.Id);
                table.ForeignKey(
                    name: "fk_order_items_order",
                    column: x => x.OrderId,
                    principalTable: "orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "idx_order_items_market_product_id",
            table: "order_items",
            column: "MarketProductId");

        migrationBuilder.CreateIndex(
            name: "idx_order_items_order_id",
            table: "order_items",
            column: "OrderId");

        migrationBuilder.CreateIndex(
            name: "idx_orders_order_group_id",
            table: "orders",
            column: "OrderGroupId");

        migrationBuilder.CreateIndex(
            name: "idx_orders_restaurant_id_status",
            table: "orders",
            columns: new[] { "RestaurantId", "Status" });

        migrationBuilder.CreateIndex(
            name: "idx_orders_scheduled_for",
            table: "orders",
            column: "ScheduledFor");

        migrationBuilder.CreateIndex(
            name: "idx_orders_scheduled_order_id",
            table: "orders",
            column: "ScheduledOrderId");

        migrationBuilder.CreateIndex(
            name: "idx_orders_status",
            table: "orders",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_orders_deleted_at",
            table: "orders",
            column: "deleted_at");

        migrationBuilder.CreateIndex(
            name: "idx_scheduled_orders_restaurant_id",
            table: "scheduled_orders",
            column: "RestaurantId");

        migrationBuilder.CreateIndex(
            name: "IX_scheduled_orders_deleted_at",
            table: "scheduled_orders",
            column: "deleted_at");

        // Cross-module FK constraints — added via raw SQL because Orders.Infrastructure has no
        // ProjectReference to Auth.Domain/Pricing.Domain (module dependency rule), so EF cannot
        // declare these as model-level HasOne/HasForeignKey relationships. Both target tables
        // already exist from prior migrations (InitAuth, PricingModuleInit).
        migrationBuilder.Sql(
            """
            ALTER TABLE orders ADD CONSTRAINT fk_orders_restaurant
                FOREIGN KEY ("RestaurantId") REFERENCES restaurants ("Id") ON DELETE RESTRICT;
            ALTER TABLE order_items ADD CONSTRAINT fk_order_items_market_product
                FOREIGN KEY ("MarketProductId") REFERENCES market_products ("Id") ON DELETE RESTRICT;
            ALTER TABLE scheduled_orders ADD CONSTRAINT fk_scheduled_orders_restaurant
                FOREIGN KEY ("RestaurantId") REFERENCES restaurants ("Id") ON DELETE RESTRICT;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE scheduled_orders DROP CONSTRAINT fk_scheduled_orders_restaurant;
            ALTER TABLE order_items DROP CONSTRAINT fk_order_items_market_product;
            ALTER TABLE orders DROP CONSTRAINT fk_orders_restaurant;
            """);

        migrationBuilder.DropTable(
            name: "order_items");

        migrationBuilder.DropTable(
            name: "scheduled_orders");

        migrationBuilder.DropTable(
            name: "orders");
    }
}
