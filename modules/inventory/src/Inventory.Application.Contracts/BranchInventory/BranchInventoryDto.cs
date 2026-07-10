using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.BranchInventory;

public class BranchInventoryDto : EntityDto<Guid>
{
    public Guid BranchId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = null!;
    public string ProductSKU { get; set; } = null!;
    public string ProductUnit { get; set; } = null!;
    public bool ProductIsActive { get; set; }
    public int QuantityOnHand { get; set; }
    public int MinimumStock { get; set; }
    public int? MaximumStock { get; set; }
    public DateTime? LastRestockedDate { get; set; }
    public DateTime? LastSoldDate { get; set; }
    public string ConcurrencyStamp { get; set; } = null!;

    /// <summary>
    /// On-hand units sitting in already-expired batches (advisory, from the best-effort
    /// batch ledger). These are not sellable — the write-off action clears them.
    /// </summary>
    public int ExpiredQuantity { get; set; }

    public bool HasExpiredStock => ExpiredQuantity > 0;
    public bool IsLowStock => QuantityOnHand <= MinimumStock;
    public bool IsOutOfStock => QuantityOnHand <= 0;
}
