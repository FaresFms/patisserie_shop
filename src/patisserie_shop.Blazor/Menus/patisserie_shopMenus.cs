namespace patisserie_shop.Blazor.Menus;

public class patisserie_shopMenus
{
    private const string Prefix = "patisserie_shop";
    public const string Home = Prefix + ".Home";

    public const string Inventory = Prefix + ".Inventory";
    public const string Categories = Inventory + ".Categories";
    public const string Suppliers = Inventory + ".Suppliers";
    public const string Products = Inventory + ".Products";
    public const string Branches = Inventory + ".Branches";
    public const string BranchInventory = Inventory + ".BranchInventory";
    public const string StockMovements = Inventory + ".StockMovements";
}
