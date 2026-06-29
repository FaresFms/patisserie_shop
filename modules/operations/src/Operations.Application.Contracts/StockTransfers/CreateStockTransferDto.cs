using System;

namespace Operations.StockTransfers;

public class CreateStockTransferDto
{
    public Guid? FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public DateTime RequestedDate { get; set; } = DateTime.Today;
    public string? Notes { get; set; }
}

public class AssignStockTransferSourceDto
{
    public Guid FromBranchId { get; set; }
}
