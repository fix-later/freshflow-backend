using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRestaurantCredit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "credit_transactions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                order_id = table.Column<Guid>(type: "uuid", nullable: true),
                type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                balance_after = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_credit_transactions", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "restaurant_credit",
            columns: table => new
            {
                restaurant_id = table.Column<Guid>(type: "uuid", nullable: false),
                credit_limit = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                outstanding_balance = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_restaurant_credit", x => x.restaurant_id);
            });

        migrationBuilder.CreateIndex(
            name: "idx_credit_transactions_created_at",
            table: "credit_transactions",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "idx_credit_transactions_order_id",
            table: "credit_transactions",
            column: "order_id");

        migrationBuilder.CreateIndex(
            name: "idx_credit_transactions_restaurant_id",
            table: "credit_transactions",
            column: "restaurant_id");

        // Cross-module FK constraints: restaurant_credit/credit_transactions reference
        // Auth's restaurants table by ID only, so Orders cannot declare model-level
        // navigations without violating module dependency boundaries.
        migrationBuilder.Sql(
            """
            ALTER TABLE restaurant_credit ADD CONSTRAINT fk_restaurant_credit_restaurant
                FOREIGN KEY (restaurant_id) REFERENCES restaurants ("Id") ON DELETE RESTRICT;
            ALTER TABLE credit_transactions ADD CONSTRAINT fk_credit_transactions_restaurant
                FOREIGN KEY (restaurant_id) REFERENCES restaurants ("Id") ON DELETE RESTRICT;
            ALTER TABLE credit_transactions ADD CONSTRAINT fk_credit_transactions_order
                FOREIGN KEY (order_id) REFERENCES orders ("Id") ON DELETE RESTRICT;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE credit_transactions DROP CONSTRAINT fk_credit_transactions_order;
            ALTER TABLE credit_transactions DROP CONSTRAINT fk_credit_transactions_restaurant;
            ALTER TABLE restaurant_credit DROP CONSTRAINT fk_restaurant_credit_restaurant;
            """);

        migrationBuilder.DropTable(
            name: "credit_transactions");

        migrationBuilder.DropTable(
            name: "restaurant_credit");
    }
}
