using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class AddDecisionLogScopeColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchId",
                table: "IntelligenceDecisionLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DaysWithoutSale",
                table: "IntelligenceDecisionLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceBranchId",
                table: "IntelligenceDecisionLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StockAtEvaluation",
                table: "IntelligenceDecisionLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetBranchId",
                table: "IntelligenceDecisionLogs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OperationsSales_BranchId",
                table: "OperationsSales",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationsSales_SaleDate",
                table: "OperationsSales",
                column: "SaleDate");

            migrationBuilder.CreateIndex(
                name: "IX_OperationsPurchaseOrders_OrderDate",
                table: "OperationsPurchaseOrders",
                column: "OrderDate");

            migrationBuilder.CreateIndex(
                name: "IX_OperationsPurchaseOrders_Status",
                table: "OperationsPurchaseOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DecisionLogs_Branch",
                table: "IntelligenceDecisionLogs",
                column: "BranchId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OperationsSales_BranchId",
                table: "OperationsSales");

            migrationBuilder.DropIndex(
                name: "IX_OperationsSales_SaleDate",
                table: "OperationsSales");

            migrationBuilder.DropIndex(
                name: "IX_OperationsPurchaseOrders_OrderDate",
                table: "OperationsPurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_OperationsPurchaseOrders_Status",
                table: "OperationsPurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_DecisionLogs_Branch",
                table: "IntelligenceDecisionLogs");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "IntelligenceDecisionLogs");

            migrationBuilder.DropColumn(
                name: "DaysWithoutSale",
                table: "IntelligenceDecisionLogs");

            migrationBuilder.DropColumn(
                name: "SourceBranchId",
                table: "IntelligenceDecisionLogs");

            migrationBuilder.DropColumn(
                name: "StockAtEvaluation",
                table: "IntelligenceDecisionLogs");

            migrationBuilder.DropColumn(
                name: "TargetBranchId",
                table: "IntelligenceDecisionLogs");
        }
    }
}
