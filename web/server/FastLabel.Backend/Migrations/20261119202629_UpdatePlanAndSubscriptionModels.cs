using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FastLabel.Backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePlanAndSubscriptionModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "StripeId",
                table: "Subscriptions",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodEnd",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CurrentPeriodStart",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ExtraPrintsBalance",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Subscriptions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentPeriodEnd",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "CurrentPeriodStart",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "ExtraPrintsBalance",
                table: "Subscriptions");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Subscriptions");

            migrationBuilder.AlterColumn<int>(
                name: "StripeId",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
