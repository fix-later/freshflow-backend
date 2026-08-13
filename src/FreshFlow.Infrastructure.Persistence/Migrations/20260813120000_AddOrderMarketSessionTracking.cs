using System;
using FreshFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260813120000_AddOrderMarketSessionTracking")]
public partial class AddOrderMarketSessionTracking : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "confirmed_at",
            table: "orders",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "market_session_id",
            table: "orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.Sql(
            """
            UPDATE orders AS o
            SET confirmed_at = confirmed.occurred_at
            FROM (
                SELECT entity_id, MIN(occurred_at) AS occurred_at
                FROM audit_logs
                WHERE action = 'order_confirmed' AND entity_type = 'order'
                GROUP BY entity_id
            ) AS confirmed
            WHERE o."Id" = confirmed.entity_id
              AND o.confirmed_at IS NULL;

            WITH membership AS (
                SELECT DISTINCT ON (link.order_id)
                       link.order_id,
                       batch.market_session_id
                FROM procurement_batch_orders AS link
                INNER JOIN procurement_batches AS batch
                    ON batch.id = link.procurement_batch_id
                WHERE batch.market_session_id IS NOT NULL
                ORDER BY link.order_id, link.updated_at DESC, batch.created_at DESC
            )
            UPDATE orders AS o
            SET market_session_id = membership.market_session_id
            FROM membership
            WHERE o."Id" = membership.order_id
              AND o.market_session_id IS NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "idx_orders_market_session_id",
            table: "orders",
            column: "market_session_id",
            filter: "market_session_id IS NOT NULL AND deleted_at IS NULL");

        migrationBuilder.AddForeignKey(
            name: "fk_orders_market_session",
            table: "orders",
            column: "market_session_id",
            principalTable: "market_sessions",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_orders_market_session",
            table: "orders");

        migrationBuilder.DropIndex(
            name: "idx_orders_market_session_id",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "confirmed_at",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "market_session_id",
            table: "orders");
    }
}
