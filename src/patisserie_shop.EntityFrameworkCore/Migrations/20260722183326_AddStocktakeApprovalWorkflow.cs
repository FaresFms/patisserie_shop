using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class AddStocktakeApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UIX_StocktakeSessions_OneDraftPerBranch",
                table: "InventoryStocktakeSessions");

            migrationBuilder.AddColumn<string>(
                name: "ReviewNotes",
                table: "InventoryStocktakeSessions",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "InventoryStocktakeSessions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedBy",
                table: "InventoryStocktakeSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByName",
                table: "InventoryStocktakeSessions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "StartedBy",
                table: "InventoryStocktakeSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StartedByName",
                table: "InventoryStocktakeSessions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "InventoryStocktakeSessions",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedBy",
                table: "InventoryStocktakeSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedByName",
                table: "InventoryStocktakeSessions",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "UIX_StocktakeSessions_OneOpenPerBranch",
                table: "InventoryStocktakeSessions",
                column: "BranchId",
                unique: true,
                filter: "\"Status\" IN ('Draft', 'PendingReview') AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UIX_StocktakeSessions_OneOpenPerBranch",
                table: "InventoryStocktakeSessions");

            migrationBuilder.DropColumn(
                name: "ReviewNotes",
                table: "InventoryStocktakeSessions");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "InventoryStocktakeSessions");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "InventoryStocktakeSessions");

            migrationBuilder.DropColumn(
                name: "ReviewedByName",
                table: "InventoryStocktakeSessions");

            migrationBuilder.DropColumn(
                name: "StartedBy",
                table: "InventoryStocktakeSessions");

            migrationBuilder.DropColumn(
                name: "StartedByName",
                table: "InventoryStocktakeSessions");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "InventoryStocktakeSessions");

            migrationBuilder.DropColumn(
                name: "SubmittedBy",
                table: "InventoryStocktakeSessions");

            migrationBuilder.DropColumn(
                name: "SubmittedByName",
                table: "InventoryStocktakeSessions");

            migrationBuilder.CreateIndex(
                name: "UIX_StocktakeSessions_OneDraftPerBranch",
                table: "InventoryStocktakeSessions",
                column: "BranchId",
                unique: true,
                filter: "\"Status\" = 'Draft' AND NOT \"IsDeleted\"");
        }
    }
}
