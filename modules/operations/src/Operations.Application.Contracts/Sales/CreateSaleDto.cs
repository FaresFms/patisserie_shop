using System;
using System.Collections.Generic;

namespace Operations.Sales;

public class CreateSaleDto
{
    public Guid BranchId { get; set; }
    /// <summary>Leave blank to auto-generate (INV-YYYY-XXXXXXXX).</summary>
    public string? InvoiceNumber { get; set; }
    public DateTime SaleDate { get; set; } = DateTime.Today;
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }
    public List<CreateSaleItemDto> Items { get; set; } = new();
}

public class CreateSaleItemDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
