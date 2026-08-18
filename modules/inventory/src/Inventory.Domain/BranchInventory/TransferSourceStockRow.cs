using System;

namespace Inventory.BranchInventory;

/// <summary>
/// Read model used to compare the safe stock available for a requested product
/// across active source branches. Missing inventory rows are projected as zero.
/// </summary>
public class TransferSourceStockRow
{
    public Guid BranchId { get; set; }
    public string BranchNameAr { get; set; } = string.Empty;
    public string BranchNameEn { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public string ProductNameAr { get; set; } = string.Empty;
    public string ProductNameEn { get; set; } = string.Empty;
    public int QuantityOnHand { get; set; }
    public int MinimumStock { get; set; }
}
