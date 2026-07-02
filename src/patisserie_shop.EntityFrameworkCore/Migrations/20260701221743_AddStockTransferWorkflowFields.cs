using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class AddStockTransferWorkflowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClosedByUserId",
                table: "OperationsStockTransfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClosureReason",
                table: "OperationsStockTransfers",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompletedByUserId",
                table: "OperationsStockTransfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ShippedByUserId",
                table: "OperationsStockTransfers",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShippedDate",
                table: "OperationsStockTransfers",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ShippedBatchBreakdown",
                table: "OperationsStockTransferItems",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationsStockTransfers_Status",
                table: "OperationsStockTransfers",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperationsStockTransfers_Status",
                table: "OperationsStockTransfers");

            migrationBuilder.DropColumn(
                name: "ClosedByUserId",
                table: "OperationsStockTransfers");

            migrationBuilder.DropColumn(
                name: "ClosureReason",
                table: "OperationsStockTransfers");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "OperationsStockTransfers");

            migrationBuilder.DropColumn(
                name: "ShippedByUserId",
                table: "OperationsStockTransfers");

            migrationBuilder.DropColumn(
                name: "ShippedDate",
                table: "OperationsStockTransfers");

            migrationBuilder.DropColumn(
                name: "ShippedBatchBreakdown",
                table: "OperationsStockTransferItems");
        }
    }
}
