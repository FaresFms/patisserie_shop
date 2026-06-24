using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class AddCashierShiftsAndSaleVoid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsVoided",
                table: "OperationsSales",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ShiftId",
                table: "OperationsSales",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VoidReason",
                table: "OperationsSales",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VoidedAt",
                table: "OperationsSales",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VoidedByUserId",
                table: "OperationsSales",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "OperationsCashierShifts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BranchId = table.Column<Guid>(type: "uuid", nullable: false),
                    CashierUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    OpeningFloat = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    CountedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    ExpectedCash = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    Variance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
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
                    table.PrimaryKey("PK_OperationsCashierShifts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OperationsSales_ShiftId",
                table: "OperationsSales",
                column: "ShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationsCashierShifts_BranchId_Status",
                table: "OperationsCashierShifts",
                columns: new[] { "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OperationsCashierShifts_CashierUserId_Status",
                table: "OperationsCashierShifts",
                columns: new[] { "CashierUserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OperationsCashierShifts");

            migrationBuilder.DropIndex(
                name: "IX_OperationsSales_ShiftId",
                table: "OperationsSales");

            migrationBuilder.DropColumn(
                name: "IsVoided",
                table: "OperationsSales");

            migrationBuilder.DropColumn(
                name: "ShiftId",
                table: "OperationsSales");

            migrationBuilder.DropColumn(
                name: "VoidReason",
                table: "OperationsSales");

            migrationBuilder.DropColumn(
                name: "VoidedAt",
                table: "OperationsSales");

            migrationBuilder.DropColumn(
                name: "VoidedByUserId",
                table: "OperationsSales");
        }
    }
}
