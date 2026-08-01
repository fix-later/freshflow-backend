using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderDeliveryAddressSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "delivery_address_id",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_address_line",
                table: "orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_latitude",
                table: "orders",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "delivery_longitude",
                table: "orders",
                type: "numeric(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_phone",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "delivery_recipient_name",
                table: "orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "delivery_address_id",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_address_line",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_latitude",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_longitude",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_phone",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "delivery_recipient_name",
                table: "orders");
        }
    }
}
