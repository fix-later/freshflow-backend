using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddMarketSessions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "market_id",
            table: "scheduled_orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "market_session_id",
            table: "procurement_batches",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "market_id",
            table: "orders",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "market_sessions",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                market_id = table.Column<Guid>(type: "uuid", nullable: false),
                hub_id = table.Column<Guid>(type: "uuid", nullable: true),
                service_date = table.Column<DateOnly>(type: "date", nullable: false),
                status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                closes_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                created_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                closed_by = table.Column<Guid>(type: "uuid", nullable: true),
                close_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                batching_completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_market_sessions", x => x.id);
            });

        migrationBuilder.Sql("""
            UPDATE orders AS o
            SET market_id = source.market_id
            FROM (
                SELECT oi."OrderId" AS order_id, MIN(mp."MarketId"::text)::uuid AS market_id
                FROM order_items AS oi
                INNER JOIN market_products AS mp ON mp."Id" = oi."MarketProductId"
                WHERE mp.deleted_at IS NULL
                GROUP BY oi."OrderId"
                HAVING COUNT(DISTINCT mp."MarketId") = 1
            ) AS source
            WHERE o."Id" = source.order_id AND o.market_id IS NULL;

            UPDATE scheduled_orders AS schedule
            SET market_id = source.market_id
            FROM (
                SELECT soi."ScheduledOrderId" AS scheduled_order_id,
                       MIN(mp."MarketId"::text)::uuid AS market_id
                FROM scheduled_order_items AS soi
                INNER JOIN market_products AS mp ON mp."Id" = soi."MarketProductId"
                WHERE mp.deleted_at IS NULL
                GROUP BY soi."ScheduledOrderId"
                HAVING COUNT(DISTINCT mp."MarketId") = 1
            ) AS source
            WHERE schedule."Id" = source.scheduled_order_id AND schedule.market_id IS NULL;

            WITH operational AS (
                SELECT COALESCE(MAX(daily_cutoff_time), TIME '22:00') AS cutoff,
                       COALESCE(BOOL_OR(batching_enabled), TRUE) AS batching_enabled
                FROM operational_settings
            ), sources AS (
                SELECT o.market_id,
                       (o."ScheduledFor" AT TIME ZONE 'Asia/Ho_Chi_Minh')::date AS service_date,
                       FALSE AS has_batch
                FROM orders AS o
                WHERE o.market_id IS NOT NULL AND o."ScheduledFor" IS NOT NULL
                  AND o."Status" IN ('Confirmed', 'Batched') AND o.deleted_at IS NULL
                UNION ALL
                SELECT b.market_id, b.batch_date, TRUE
                FROM procurement_batches AS b
                WHERE b.deleted_at IS NULL AND b.status <> 'Cancelled'
            ), grouped AS (
                SELECT market_id, service_date, BOOL_OR(has_batch) AS has_batch
                FROM sources
                GROUP BY market_id, service_date
            ), prepared AS (
                SELECT grouped.*,
                       hub.id AS hub_id,
                       ((grouped.service_date - 1) + operational.cutoff)
                           AT TIME ZONE 'Asia/Ho_Chi_Minh' AS closes_at,
                       operational.batching_enabled
                FROM grouped
                CROSS JOIN operational
                LEFT JOIN LATERAL (
                    SELECT h.id
                    FROM hubs AS h
                    WHERE h.market_id = grouped.market_id
                      AND h.is_active = TRUE AND h.deleted_at IS NULL
                    LIMIT 1
                ) AS hub ON TRUE
            )
            INSERT INTO market_sessions (
                id, market_id, hub_id, service_date, status, closes_at, created_source,
                closed_at, close_reason, batching_completed_at, created_at, updated_at)
            SELECT gen_random_uuid(),
                   prepared.market_id,
                   prepared.hub_id,
                   prepared.service_date,
                   CASE
                       WHEN prepared.has_batch OR prepared.closes_at <= NOW() THEN 'Closed'
                       WHEN prepared.batching_enabled
                            AND prepared.hub_id IS NOT NULL
                            AND EXISTS (
                                SELECT 1
                                FROM user_market_assignments AS assignment
                                INNER JOIN users AS u ON u."Id" = assignment."UserId"
                                INNER JOIN roles AS r ON r."Id" = u."RoleId"
                                WHERE assignment."MarketId" = prepared.market_id
                                  AND u."IsActive" = TRUE AND u."DeletedAt" IS NULL
                                  AND r."Name" = 'market_agent')
                            AND EXISTS (
                                SELECT 1
                                FROM vehicles AS vehicle
                                WHERE vehicle.hub_id = prepared.hub_id
                                  AND vehicle.is_available = TRUE AND vehicle.deleted_at IS NULL
                                  AND NOT EXISTS (
                                      SELECT 1
                                      FROM delivery_routes AS route
                                      WHERE route.vehicle_id = vehicle.id
                                        AND route.service_date = prepared.service_date
                                        AND route.status <> 'cancelled'
                                        AND route.deleted_at IS NULL))
                           THEN 'Open'
                       ELSE 'Draft'
                   END,
                   prepared.closes_at,
                   'Auto',
                   CASE WHEN prepared.has_batch OR prepared.closes_at <= NOW() THEN NOW() END,
                   CASE WHEN prepared.has_batch THEN 'migration_existing_batch'
                        WHEN prepared.closes_at <= NOW() THEN 'migration_cutoff_passed' END,
                   CASE WHEN prepared.has_batch THEN NOW() END,
                   NOW(),
                   NOW()
            FROM prepared
            ON CONFLICT DO NOTHING;

            WITH ranked AS (
                SELECT b.id,
                       session.id AS session_id,
                       ROW_NUMBER() OVER (
                           PARTITION BY b.market_id, b.batch_date
                           ORDER BY b.created_at DESC, b.id DESC) AS row_number
                FROM procurement_batches AS b
                INNER JOIN market_sessions AS session
                    ON session.market_id = b.market_id
                   AND session.service_date = b.batch_date
                   AND session.deleted_at IS NULL
                WHERE b.deleted_at IS NULL AND b.status <> 'Cancelled'
            )
            UPDATE procurement_batches AS batch
            SET market_session_id = ranked.session_id
            FROM ranked
            WHERE batch.id = ranked.id AND ranked.row_number = 1;
            """);

        migrationBuilder.CreateIndex(
            name: "ux_procurement_batches_market_session_active",
            table: "procurement_batches",
            column: "market_session_id",
            unique: true,
            filter: "market_session_id IS NOT NULL AND status <> 'Cancelled' AND deleted_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "idx_orders_market_scheduled_for",
            table: "orders",
            columns: new[] { "market_id", "ScheduledFor" },
            filter: "market_id IS NOT NULL AND deleted_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "idx_market_sessions_status_closes_at",
            table: "market_sessions",
            columns: new[] { "status", "closes_at" },
            filter: "deleted_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ux_market_sessions_market_date_active",
            table: "market_sessions",
            columns: new[] { "market_id", "service_date" },
            unique: true,
            filter: "deleted_at IS NULL");

        migrationBuilder.AddForeignKey(
            name: "fk_procurement_batches_market_session",
            table: "procurement_batches",
            column: "market_session_id",
            principalTable: "market_sessions",
            principalColumn: "id",
            onDelete: ReferentialAction.Restrict);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "fk_procurement_batches_market_session",
            table: "procurement_batches");

        migrationBuilder.DropTable(
            name: "market_sessions");

        migrationBuilder.DropIndex(
            name: "ux_procurement_batches_market_session_active",
            table: "procurement_batches");

        migrationBuilder.DropIndex(
            name: "idx_orders_market_scheduled_for",
            table: "orders");

        migrationBuilder.DropColumn(
            name: "market_id",
            table: "scheduled_orders");

        migrationBuilder.DropColumn(
            name: "market_session_id",
            table: "procurement_batches");

        migrationBuilder.DropColumn(
            name: "market_id",
            table: "orders");
    }
}
