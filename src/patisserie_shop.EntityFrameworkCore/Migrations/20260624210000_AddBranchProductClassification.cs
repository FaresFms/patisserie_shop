using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace patisserie_shop.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchProductClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsProducible",
                table: "InventoryProducts",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill existing products as purchasable + sellable finished goods so the
            // POS / PO pickers (now filtered by these flags) keep showing them. New rows use
            // the entity code-defaults; these defaultValues only seed pre-existing rows.
            migrationBuilder.AddColumn<bool>(
                name: "IsPurchasable",
                table: "InventoryProducts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSellable",
                table: "InventoryProducts",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductType",
                table: "InventoryProducts",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "FinishedGood");

            migrationBuilder.AddColumn<string>(
                name: "BranchType",
                table: "InventoryBranches",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "SalesBranch");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsProducible",
                table: "InventoryProducts");

            migrationBuilder.DropColumn(
                name: "IsPurchasable",
                table: "InventoryProducts");

            migrationBuilder.DropColumn(
                name: "IsSellable",
                table: "InventoryProducts");

            migrationBuilder.DropColumn(
                name: "ProductType",
                table: "InventoryProducts");

            migrationBuilder.DropColumn(
                name: "BranchType",
                table: "InventoryBranches");
        }
    }
}
