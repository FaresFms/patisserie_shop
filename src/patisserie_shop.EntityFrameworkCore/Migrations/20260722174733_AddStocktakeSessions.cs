using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class AddStocktakeSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InventoryStocktakeSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    SnapshotAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    MovementReferenceId = table.Column<Guid>(type: "uuid", nullable: true),
                    TotalLineCount = table.Column<int>(type: "integer", nullable: false),
                    CountedLineCount = table.Column<int>(type: "integer", nullable: false),
                    DifferenceLineCount = table.Column<int>(type: "integer", nullable: false),
                    AdjustedLineCount = table.Column<int>(type: "integer", nullable: false),
                    MatchedLineCount = table.Column<int>(type: "integer", nullable: false),
                    WriteOffLineCount = table.Column<int>(type: "integer", nullable: false),
                    ManualAdjustmentLineCount = table.Column<int>(type: "integer", nullable: false),
                    ShortageQuantity = table.Column<int>(type: "integer", nullable: false),
                    OverageQuantity = table.Column<int>(type: "integer", nullable: false),
                    ExtraProperties = table.Column<string>(type: "text", nullable: false),
                    ConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CreationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModificationTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    LastModifierId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeleterId = table.Column<Guid>(type: "uuid", nullable: true),
                    DeletionTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryStocktakeSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InventoryStocktakeLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InventoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ProductSku = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProductUnit = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ExpectedQuantity = table.Column<int>(type: "integer", nullable: false),
                    IsPerishable = table.Column<bool>(type: "boolean", nullable: false),
                    InventoryConcurrencyStamp = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CountedQuantity = table.Column<int>(type: "integer", nullable: true),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    ReasonNotes = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ProductionDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryStocktakeLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryStocktakeLines_InventoryStocktakeSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "InventoryStocktakeSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UIX_StocktakeLines_Session_Inventory",
                table: "InventoryStocktakeLines",
                columns: new[] { "SessionId", "InventoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeSessions_Branch_Status",
                table: "InventoryStocktakeSessions",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StocktakeSessions_Snapshot",
                table: "InventoryStocktakeSessions",
                column: "SnapshotAt");

            migrationBuilder.CreateIndex(
                name: "UIX_StocktakeSessions_OneDraftPerBranch",
                table: "InventoryStocktakeSessions",
                column: "BranchId",
                unique: true,
                filter: "\"Status\" = 'Draft' AND NOT \"IsDeleted\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InventoryStocktakeLines");

            migrationBuilder.DropTable(
                name: "InventoryStocktakeSessions");
        }
    }
}
