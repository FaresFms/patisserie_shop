using System;
using System.Collections.Generic;

namespace Operations.Cashier;

public class RecordCashierSaleDto
{
    public Guid BranchId { get; set; }
    public List<RecordCashierSaleLineDto> Lines { get; set; } = new();

    /// <summary>Cash handed over by the customer. Cash-only sales require the full amount.</summary>
    public decimal? CashTendered { get; set; }

    /// <summary>Legacy compatibility flag. Expired stock can no longer be sold.</summary>
    [Obsolete("Expired stock sales are blocked and cannot be acknowledged.")]
    public bool AcknowledgeExpiredStock { get; set; }
}

public class RecordCashierSaleLineDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
