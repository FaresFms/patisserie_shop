using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Production.Orders;

public class ScheduleProductionOrderDto
{
    [Required, StringLength(64)]
    public string WorkCenterCode { get; set; } = null!;

    [Required, StringLength(64)]
    public string ShiftCode { get; set; } = null!;

    public DateTime ScheduledStartTime { get; set; }
    public DateTime ScheduledEndTime { get; set; }

    [Required]
    public Guid OperatorUserId { get; set; }

    [Required, StringLength(128)]
    public string OperatorName { get; set; } = null!;
}

public class ProductionQualityActionDto
{
    [StringLength(1024)]
    public string? Reason { get; set; }
}

public class ProductionIngredientLotDto
{
    public Guid IngredientProductId { get; set; }
    public string? IngredientName { get; set; }
    public Guid BatchId { get; set; }
    public string BatchNumber { get; set; } = null!;
    public DateTime ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class CreateSubProductionOrdersResultDto
{
    public int CreatedCount => Orders.Count;
    public List<ProductionOrderDto> Orders { get; set; } = new();
}
