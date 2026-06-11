using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2DemandIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeadTimeDays",
                table: "InventorySuppliers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ActionMode",
                table: "IntelligenceInventoryRules",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                table: "IntelligenceDecisionLogs",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OutcomeEvaluatedAt",
                table: "IntelligenceDecisionLogs",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "IntelligenceProductVelocities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    AvgDailySales7 = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    AvgDailySales30 = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: false),
                    QuantitySold30 = table.Column<int>(type: "integer", nullable: false),
                    Revenue30 = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    AbcClass = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: false),
                    ComputedAtUtc = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntelligenceProductVelocities", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVelocities_Branch",
                table: "IntelligenceProductVelocities",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductVelocities_Product_Branch",
                table: "IntelligenceProductVelocities",
                columns: new[] { "ProductId", "BranchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntelligenceProductVelocities");

            migrationBuilder.DropColumn(
                name: "LeadTimeDays",
                table: "InventorySuppliers");

            migrationBuilder.DropColumn(
                name: "ActionMode",
                table: "IntelligenceInventoryRules");

            migrationBuilder.DropColumn(
                name: "Outcome",
                table: "IntelligenceDecisionLogs");

            migrationBuilder.DropColumn(
                name: "OutcomeEvaluatedAt",
                table: "IntelligenceDecisionLogs");
        }
    }
}
