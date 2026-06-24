using System;

namespace Operations.Cashier;

public class VoidSaleDto
{
    public Guid SaleId { get; set; }
    public string? Reason { get; set; }
}
