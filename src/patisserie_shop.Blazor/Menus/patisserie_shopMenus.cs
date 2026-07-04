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
    public const string WasteAnalytics = Inventory + ".WasteAnalytics";

    public const string Operations = Prefix + ".Operations";
    public const string PurchaseOrders = Operations + ".PurchaseOrders";
    public const string Sales = Operations + ".Sales";
    public const string StockTransfers = Operations + ".StockTransfers";
    public const string SalesAnalytics = Operations + ".SalesAnalytics";
    public const string Cashier = Operations + ".Cashier";
    public const string CashierShifts = Operations + ".CashierShifts";
    public const string CashierAssignments = Operations + ".CashierAssignments";
    public const string AddCashier = Operations + ".AddCashier";

    public const string Intelligence = Prefix + ".Intelligence";
    public const string InventoryRules = Intelligence + ".InventoryRules";
    public const string DecisionLog = Intelligence + ".DecisionLog";
    public const string ProductVelocity = Intelligence + ".ProductVelocity";
    public const string ReorderCalendar = Intelligence + ".ReorderCalendar";

    public const string Setup = Prefix + ".Setup";
    public const string ShopSettings = Setup + ".ShopSettings";

    public const string Production = Prefix + ".Production";
    public const string ProductionDashboard = Production + ".Dashboard";
    public const string ProductionFormulas = Production + ".Formulas";
    public const string ProductionMyRequests = Production + ".MyRequests";
    public const string ProductionBranchRequests = Production + ".BranchRequests";
    public const string ProductionPlans = Production + ".Plans";
    public const string ProductionCook = Production + ".Cook";
    public const string ProductionDispatch = Production + ".Dispatch";
    public const string ProductionWaste = Production + ".Waste";
    public const string ProductionAnalytics = Production + ".Analytics";
}
