using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class TrackProductionPlanningAndDispatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "BranchProductionRequestId",
                table: "ProductionOrderAllocations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DispatchedQuantity",
                table: "ProductionOrderAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "LostQuantity",
                table: "ProductionOrderAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PlannedQuantity",
                table: "ProductionBranchRequestItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Preserve historical workflow state. Requests that had already entered a
            // plan must stay reserved after the new explicit planning column is added;
            // otherwise the next plan would pick them up a second time. For old rows
            // without a planning status, at least preserve the quantity already fulfilled.
            migrationBuilder.Sql("""
                UPDATE "ProductionBranchRequestItems" AS item
                SET "PlannedQuantity" = CASE
                    WHEN request."Status" IN ('PartiallyPlanned', 'Planned', 'PartiallyFulfilled', 'Fulfilled')
                        THEN item."ApprovedQuantity"
                    ELSE item."FulfilledQuantity"
                END
                FROM "ProductionBranchRequests" AS request
                WHERE request."Id" = item."RequestId";

                UPDATE "ProductionOrderAllocations" AS allocation
                SET "BranchProductionRequestId" = item."RequestId",
                    "DispatchedQuantity" = allocation."FulfilledQuantity"
                FROM "ProductionBranchRequestItems" AS item
                WHERE item."Id" = allocation."BranchProductionRequestItemId";
                """);

            migrationBuilder.CreateTable(
                name: "ProductionOrderDispatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    StockTransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    DestinationBranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ShippedQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "integer", nullable: false),
                    LostQuantity = table.Column<int>(type: "integer", nullable: false),
                    DispatchedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderDispatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrderDispatches_ProductionOrders_ProductionOrderId",
                        column: x => x.ProductionOrderId,
                        principalTable: "ProductionOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductionOrderDispatchLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderDispatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductionOrderAllocationId = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchProductionRequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    BranchProductionRequestItemId = table.Column<Guid>(type: "uuid", nullable: true),
                    ShippedQuantity = table.Column<int>(type: "integer", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "integer", nullable: false),
                    LostQuantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductionOrderDispatchLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductionOrderDispatchLines_ProductionOrderDispatches_Prod~",
                        column: x => x.ProductionOrderDispatchId,
                        principalTable: "ProductionOrderDispatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderDispatches_BranchCompleted",
                table: "ProductionOrderDispatches",
                columns: new[] { "DestinationBranchId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderDispatches_Order",
                table: "ProductionOrderDispatches",
                column: "ProductionOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderDispatches_Transfer",
                table: "ProductionOrderDispatches",
                column: "StockTransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderDispatchLines_Allocation",
                table: "ProductionOrderDispatchLines",
                column: "ProductionOrderAllocationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderDispatchLines_Dispatch",
                table: "ProductionOrderDispatchLines",
                column: "ProductionOrderDispatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductionOrderDispatchLines_RequestItem",
                table: "ProductionOrderDispatchLines",
                column: "BranchProductionRequestItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductionOrderDispatchLines");

            migrationBuilder.DropTable(
                name: "ProductionOrderDispatches");

            migrationBuilder.DropColumn(
                name: "BranchProductionRequestId",
                table: "ProductionOrderAllocations");

            migrationBuilder.DropColumn(
                name: "DispatchedQuantity",
                table: "ProductionOrderAllocations");

            migrationBuilder.DropColumn(
                name: "LostQuantity",
                table: "ProductionOrderAllocations");

            migrationBuilder.DropColumn(
                name: "PlannedQuantity",
                table: "ProductionBranchRequestItems");
        }
    }
}
