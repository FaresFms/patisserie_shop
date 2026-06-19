using System;
using System.Collections.Generic;

namespace Operations.Cashier;

/// <summary>
/// Result of ringing up a cash sale — enough to render an on-screen receipt and show the
/// change due.
/// </summary>
public class CashierSaleResultDto
{
    public Guid SaleId { get; set; }
    public string InvoiceNumber { get; set; } = null!;
    public DateTime SaleDate { get; set; }
    public decimal Total { get; set; }

    /// <summary>CashTendered − Total when cash was tendered; null otherwise.</summary>
    public decimal? ChangeDue { get; set; }

    public List<ReceiptLineDto> Lines { get; set; } = new();
}

public class ReceiptLineDto
{
    public string ProductName { get; set; } = null!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Subtotal { get; set; }
}
