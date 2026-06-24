using System;
using System.Collections.Generic;

namespace Operations.Cashier;

public class RecordCashierSaleDto
{
    public Guid BranchId { get; set; }
    public List<RecordCashierSaleLineDto> Lines { get; set; } = new();

    /// <summary>Cash handed over by the customer; null when not captured. Used to compute change.</summary>
    public decimal? CashTendered { get; set; }
}

public class RecordCashierSaleLineDto
{
    public Guid ProductId { get; set; }
    public int Quantity { get; set; }
}
