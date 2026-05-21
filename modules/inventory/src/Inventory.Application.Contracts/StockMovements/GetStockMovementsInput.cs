using System;
using Volo.Abp.Application.Dtos;

namespace Inventory.StockMovements;

public class GetStockMovementsInput : PagedAndSortedResultRequestDto
{
    public Guid? BranchId { get; set; }
    public Guid? ProductId { get; set; }
    public string? MovementType { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Filter { get; set; }
}
