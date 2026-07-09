using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddNotificationSendStatus : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "attempt_count",
            table: "notifications",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "failed_reason",
            table: "notifications",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "last_attempt_at",
            table: "notifications",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "send_status",
            table: "notifications",
            type: "character varying(20)",
            maxLength: 20,
            nullable: false,
            defaultValue: "pending");

        migrationBuilder.CreateIndex(
            name: "idx_notifications_retry_scan",
            table: "notifications",
            columns: new[] { "attempt_count", "last_attempt_at" },
            filter: "send_status = 'failed'");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_notifications_retry_scan",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "attempt_count",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "failed_reason",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "last_attempt_at",
            table: "notifications");

        migrationBuilder.DropColumn(
            name: "send_status",
            table: "notifications");
    }
}
