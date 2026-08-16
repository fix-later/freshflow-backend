using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddProcurementBatchItemAssignments : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "assigned_agent_user_id",
            table: "procurement_batch_items",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "assigned_at",
            table: "procurement_batch_items",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE procurement_batch_items AS item
            SET assigned_agent_user_id = batch.assigned_agent_user_id,
                assigned_at = batch.assigned_at
            FROM procurement_batches AS batch
            WHERE batch.id = item.procurement_batch_id
              AND batch.assigned_agent_user_id IS NOT NULL
            """);

        migrationBuilder.CreateIndex(
            name: "idx_procurement_batch_items_assigned_agent",
            table: "procurement_batch_items",
            column: "assigned_agent_user_id",
            filter: "\"assigned_agent_user_id\" IS NOT NULL");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "idx_procurement_batch_items_assigned_agent",
            table: "procurement_batch_items");

        migrationBuilder.DropColumn(
            name: "assigned_agent_user_id",
            table: "procurement_batch_items");

        migrationBuilder.DropColumn(
            name: "assigned_at",
            table: "procurement_batch_items");
    }
}
