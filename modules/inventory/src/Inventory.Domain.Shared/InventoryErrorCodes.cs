namespace Inventory;

public static class InventoryErrorCodes
{
    public const string DuplicateProductSku = "Inventory:Products:DuplicateSku";
    public const string DuplicateBranchName = "Inventory:Branches:DuplicateName";
    public const string DuplicateCategoryName = "Inventory:Categories:DuplicateName";
    public const string DuplicateSupplierName = "Inventory:Suppliers:DuplicateName";
    public const string InvalidSupplierLeadTime = "Inventory:Suppliers:InvalidLeadTime";
    public const string DuplicateBranchInventory = "Inventory:BranchInventory:Duplicate";
    public const string BranchInventoryConcurrency = "Inventory:BranchInventory:Concurrency";
    public const string BranchIdRequired = "Inventory:BranchInventory:BranchIdRequired";
    public const string InvalidMovementType = "Inventory:BranchInventory:InvalidMovementType";
    public const string NegativeStock = "Inventory:BranchInventory:NegativeStock";
    public const string BranchAccessDenied = "Inventory:BranchInventory:BranchAccessDenied";
    public const string InvalidStockLimits = "Inventory:BranchInventory:InvalidStockLimits";
    public const string NoExpiredStockToWriteOff = "Inventory:BranchInventory:NoExpiredStockToWriteOff";
    public const string StockMovementNotFound = "Inventory:StockMovements:NotFound";
    public const string InvalidShelfLifeDays = "Inventory:Products:InvalidShelfLifeDays";
    public const string InvalidBatchQuantity = "Inventory:StockBatches:InvalidQuantity";
    public const string InvalidBatchConsumeQuantity = "Inventory:StockBatches:InvalidConsumeQuantity";
    public const string InvalidBatchSourceType = "Inventory:StockBatches:InvalidSourceType";
    public const string InvalidBranchType = "Inventory:Branches:InvalidBranchType";
    public const string InvalidProductType = "Inventory:Products:InvalidProductType";
}
