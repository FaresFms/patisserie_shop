namespace Inventory;

public static class InventoryErrorCodes
{
    public const string DuplicateProductSku = "Inventory:Products:DuplicateSku";
    public const string DuplicateBranchInventory = "Inventory:BranchInventory:Duplicate";
    public const string InvalidMovementType = "Inventory:BranchInventory:InvalidMovementType";
    public const string NegativeStock = "Inventory:BranchInventory:NegativeStock";
    public const string BranchAccessDenied = "Inventory:BranchInventory:BranchAccessDenied";
    public const string InvalidStockLimits = "Inventory:BranchInventory:InvalidStockLimits";
}
