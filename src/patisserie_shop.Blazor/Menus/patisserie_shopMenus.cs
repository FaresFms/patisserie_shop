namespace patisserie_shop.Blazor.Menus;

public class patisserie_shopMenus
{
    private const string Prefix = "patisserie_shop";
    public const string Home = Prefix + ".Home";

    public const string Inventory = Prefix + ".Inventory";
    public const string InventoryDashboard = Inventory + ".Dashboard";
    public const string Categories = Inventory + ".Categories";
    public const string Suppliers = Inventory + ".Suppliers";
    public const string Products = Inventory + ".Products";
    public const string Branches = Inventory + ".Branches";
    public const string BranchInventory = Inventory + ".BranchInventory";
    public const string StockMovements = Inventory + ".StockMovements";
    public const string StockBatches = Inventory + ".StockBatches";

    public const string Operations = Prefix + ".Operations";
    public const string PurchaseOrders = Operations + ".PurchaseOrders";
    public const string Sales = Operations + ".Sales";
    public const string StockTransfers = Operations + ".StockTransfers";
    public const string SalesAnalytics = Operations + ".SalesAnalytics";

    public const string Intelligence = Prefix + ".Intelligence";
    public const string InventoryRules = Intelligence + ".InventoryRules";
    public const string DecisionLog = Intelligence + ".DecisionLog";
    public const string ProductVelocity = Intelligence + ".ProductVelocity";
}
