using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddOrderClaims : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "order_claims",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(14,2)", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DecisionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                RefundTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_order_claims", x => x.Id);
                table.CheckConstraint("ck_order_claims_amount_positive", "\"Amount\" > 0");
                table.ForeignKey(
                    name: "fk_order_claims_order",
                    column: x => x.OrderId,
                    principalTable: "orders",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "fk_order_claims_refund_transaction",
                    column: x => x.RefundTransactionId,
                    principalTable: "credit_transactions",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "idx_order_claims_order_id",
            table: "order_claims",
            column: "OrderId");

        migrationBuilder.CreateIndex(
            name: "idx_order_claims_restaurant_status_created_at",
            table: "order_claims",
            columns: new[] { "RestaurantId", "Status", "CreatedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_order_claims_deleted_at",
            table: "order_claims",
            column: "deleted_at");

        migrationBuilder.CreateIndex(
            name: "ux_order_claims_refund_transaction_id",
            table: "order_claims",
            column: "RefundTransactionId",
            unique: true,
            filter: "\"RefundTransactionId\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "order_claims");
    }
}
