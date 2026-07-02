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

    // Cashier / cash drawer
    public const string NoOpenShift = "Operations:Cashier:NoOpenShift";
    public const string ShiftAlreadyOpen = "Operations:Cashier:ShiftAlreadyOpen";
    public const string ShiftAlreadyClosed = "Operations:Cashier:ShiftAlreadyClosed";
    public const string InvalidOpeningFloat = "Operations:Cashier:InvalidOpeningFloat";
    public const string VoidWindowExpired = "Operations:Cashier:VoidWindowExpired";
    public const string SaleAlreadyVoided = "Operations:Cashier:SaleAlreadyVoided";
    public const string BranchNotAssignedToCashier = "Operations:Cashier:BranchNotAssignedToCashier";
    public const string BranchNotManagedByYou = "Operations:Cashier:BranchNotManagedByYou";
    public const string CashierSaleAccessDenied = "Operations:Cashier:SaleAccessDenied";

    // Stock transfers
    public const string TransferInvalidStatusTransition = "Operations:Transfers:InvalidStatusTransition";
    public const string CannotModifyTransferAfterDraft = "Operations:Transfers:CannotModifyAfterDraft";
    public const string CannotSubmitEmptyTransfer = "Operations:Transfers:CannotSubmitEmptyTransfer";
    public const string TransferItemNotFound = "Operations:Transfers:ItemNotFound";
    public const string DuplicateProductInTransfer = "Operations:Transfers:DuplicateProduct";
    public const string SameSourceAndDestination = "Operations:Transfers:SameBranch";
    public const string TransferSourceBranchRequired = "Operations:Transfers:SourceBranchRequired";
    public const string InsufficientStockAtSource = "Operations:Transfers:InsufficientStock";
    public const string TransferInvalidQuantity = "Operations:Transfers:InvalidQuantity";
    public const string CannotApproveTransferItem = "Operations:Transfers:CannotApproveItem";
    public const string TransferRejectionReasonRequired = "Operations:Transfers:RejectionReasonRequired";
    public const string TransferReceivedExceedsShipped = "Operations:Transfers:ReceivedExceedsShipped";
}
