namespace Operations;

public static class OperationsErrorCodes
{
    // Purchase orders
    public const string InvalidStatusTransition = "Operations:PurchaseOrders:InvalidStatusTransition";
    public const string CannotModifyAfterDraft = "Operations:PurchaseOrders:CannotModifyAfterDraft";
    public const string CannotSubmitEmptyOrder = "Operations:PurchaseOrders:CannotSubmitEmptyOrder";
    public const string PurchaseOrderItemNotFound = "Operations:PurchaseOrders:ItemNotFound";
    public const string ReceivedExceedsOrdered = "Operations:PurchaseOrders:ReceivedExceedsOrdered";
    public const string DuplicateProductInOrder = "Operations:PurchaseOrders:DuplicateProduct";
    public const string InvalidQuantity = "Operations:PurchaseOrders:InvalidQuantity";
    public const string InvalidPrice = "Operations:PurchaseOrders:InvalidPrice";

    // Sales
    public const string DuplicateInvoiceNumber = "Operations:Sales:DuplicateInvoiceNumber";
    public const string CannotRecordEmptySale = "Operations:Sales:CannotRecordEmptySale";
    public const string DuplicateProductInSale = "Operations:Sales:DuplicateProduct";
    public const string InsufficientStock = "Operations:Sales:InsufficientStock";
    public const string NoInventoryRow = "Operations:Sales:NoInventoryRow";
}
