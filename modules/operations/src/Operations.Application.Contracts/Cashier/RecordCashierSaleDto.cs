using System;
using System.Collections.Generic;

namespace Operations.Cashier;

public class RecordCashierSaleDto
{
    public Guid BranchId { get; set; }
    public List<RecordCashierSaleLineDto> Lines { get; set; } = new();

    /// <summary>Cash handed over by the customer; null when not captured. Used to compute change.</summary>
    public decimal? CashTendered { get; set; }

    /// <summary>
    /// Set after the cashier confirms the expired-stock warning: the sale needs more
    /// units than the branch's non-expired stock covers, and the cashier chose to
    /// sell anyway (e.g. clearing old stock at a discount).
    /// </summary>
    public bool AcknowledgeExpiredStock { get; set; }
}

public class RecordCashierSaleLineDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
