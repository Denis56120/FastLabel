using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FastLabel.Backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdatePlanAndSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Plans_Texts_DescriptionJsonTextId",
                table: "Plans");

            migrationBuilder.DropForeignKey(
                name: "FK_Subscriptions_SubscriptionTypes_SubscriptionTypeId",
                table: "Subscriptions");

            migrationBuilder.DropTable(
                name: "SubscriptionTypes");

            migrationBuilder.DropIndex(
                name: "IX_Subscriptions_SubscriptionTypeId",
                table: "Subscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Plans_DescriptionJsonTextId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "SubscriptionTypeId",
                table: "Subscriptions");

            migrationBuilder.RenameColumn(
                name: "DescriptionJsonTextId",
                table: "Plans",
                newName: "SortOrder");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "Plans",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyPrice",
                table: "Plans",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "MonthlyPrintsIncluded",
                table: "Plans",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "StripeMonthlyPriceId",
                table: "Plans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeProductId",
                table: "Plans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeTopUpPriceId",
                table: "Plans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StripeYearlyPriceId",
                table: "Plans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TopUpPrice",
                table: "Plans",
                type: "numeric(10,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TopUpPrintsIncluded",
                table: "Plans",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Plans",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "YearlyPrice",
                table: "Plans",
                type: "numeric(10,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Code",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MonthlyPrice",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "MonthlyPrintsIncluded",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripeMonthlyPriceId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripeProductId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripeTopUpPriceId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "StripeYearlyPriceId",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "TopUpPrice",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "TopUpPrintsIncluded",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Plans");

            migrationBuilder.DropColumn(
                name: "YearlyPrice",
                table: "Plans");

            migrationBuilder.RenameColumn(
                name: "SortOrder",
                table: "Plans",
                newName: "DescriptionJsonTextId");

            migrationBuilder.AddColumn<int>(
                name: "SubscriptionTypeId",
                table: "Subscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "SubscriptionTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    NameTextId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionTypes_Texts_NameTextId",
                        column: x => x.NameTextId,
                        principalTable: "Texts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_SubscriptionTypeId",
                table: "Subscriptions",
                column: "SubscriptionTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_DescriptionJsonTextId",
                table: "Plans",
                column: "DescriptionJsonTextId");

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionTypes_NameTextId",
                table: "SubscriptionTypes",
                column: "NameTextId");

            migrationBuilder.AddForeignKey(
                name: "FK_Plans_Texts_DescriptionJsonTextId",
                table: "Plans",
                column: "DescriptionJsonTextId",
                principalTable: "Texts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Subscriptions_SubscriptionTypes_SubscriptionTypeId",
                table: "Subscriptions",
                column: "SubscriptionTypeId",
                principalTable: "SubscriptionTypes",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
