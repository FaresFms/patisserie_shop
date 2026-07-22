using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class EnforceUsableExpiryBatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SoldBatchBreakdown",
                table: "OperationsSaleItems",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SoldBatchBreakdown",
                table: "OperationsSaleItems");
        }
    }
}
