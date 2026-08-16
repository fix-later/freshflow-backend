using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class EnforceActiveNotificationDeviceOwnership : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_notification_devices_user_token_active",
            table: "notification_devices");

        migrationBuilder.Sql(
            """
            WITH ranked AS (
                SELECT id,
                       ROW_NUMBER() OVER (
                           PARTITION BY token
                           ORDER BY updated_at DESC, created_at DESC, id DESC) AS row_number
                FROM notification_devices
                WHERE revoked_at IS NULL
            )
            UPDATE notification_devices AS device
            SET revoked_at = CURRENT_TIMESTAMP,
                updated_at = CURRENT_TIMESTAMP
            FROM ranked
            WHERE device.id = ranked.id
              AND ranked.row_number > 1;

            WITH ranked AS (
                SELECT id,
                       ROW_NUMBER() OVER (
                           PARTITION BY device_id
                           ORDER BY updated_at DESC, created_at DESC, id DESC) AS row_number
                FROM notification_devices
                WHERE device_id IS NOT NULL
                  AND revoked_at IS NULL
            )
            UPDATE notification_devices AS device
            SET revoked_at = CURRENT_TIMESTAMP,
                updated_at = CURRENT_TIMESTAMP
            FROM ranked
            WHERE device.id = ranked.id
              AND ranked.row_number > 1;
            """);

        migrationBuilder.CreateIndex(
            name: "ux_notification_devices_device_id_active",
            table: "notification_devices",
            column: "device_id",
            unique: true,
            filter: "device_id IS NOT NULL AND revoked_at IS NULL");

        migrationBuilder.CreateIndex(
            name: "ux_notification_devices_token_active",
            table: "notification_devices",
            column: "token",
            unique: true,
            filter: "revoked_at IS NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "ux_notification_devices_device_id_active",
            table: "notification_devices");

        migrationBuilder.DropIndex(
            name: "ux_notification_devices_token_active",
            table: "notification_devices");

        migrationBuilder.CreateIndex(
            name: "ux_notification_devices_user_token_active",
            table: "notification_devices",
            columns: new[] { "user_id", "token" },
            unique: true,
            filter: "revoked_at IS NULL");
    }
}
