using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreshFlow.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class AddRestaurantProfileFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Address",
            table: "restaurants",
            type: "character varying(500)",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ContactPerson",
            table: "restaurants",
            type: "character varying(200)",
            maxLength: 200,
            nullable: true);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "PickupEnd",
            table: "restaurants",
            type: "time without time zone",
            nullable: true);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "PickupStart",
            table: "restaurants",
            type: "time without time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Address",
            table: "restaurants");

        migrationBuilder.DropColumn(
            name: "ContactPerson",
            table: "restaurants");

        migrationBuilder.DropColumn(
            name: "PickupEnd",
            table: "restaurants");

        migrationBuilder.DropColumn(
            name: "PickupStart",
            table: "restaurants");
    }
}
