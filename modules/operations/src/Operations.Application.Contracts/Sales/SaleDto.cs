using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Operations.Sales;

public class SaleDto : EntityDto<Guid>
{
    public string InvoiceNumber { get; set; } = null!;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = null!;
    public DateTime SaleDate { get; set; }
    public decimal TotalAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }
    public DateTime CreationTime { get; set; }
    public Guid? CreatorId { get; set; }
    public string? CreatorUserName { get; set; }
    public int ItemCount { get; set; }
    public List<SaleItemDto> Items { get; set; } = new();
}
