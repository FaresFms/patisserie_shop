using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Production.Formulas;

public class CreateProductionFormulaItemDto
{
    [Required]
    public Guid IngredientProductId { get; set; }

    [Range(1, int.MaxValue)]
    public int Quantity { get; set; }

    [Range(0, 100)]
    public decimal LossPercent { get; set; }

    public int SortOrder { get; set; }
}

public class CreateProductionFormulaDto
{
    [Required]
    public Guid FinishedProductId { get; set; }

    [Required]
    [StringLength(128)]
    public string FormulaName { get; set; } = null!;

    [Range(1, int.MaxValue)]
    public int Version { get; set; } = 1;

    [Range(1, int.MaxValue)]
    public int OutputQuantity { get; set; } = 1;

    [Range(0, 100)]
    public decimal ExpectedWastePercent { get; set; }

    [Range(0, double.MaxValue)]
    public decimal LaborCostPerBatch { get; set; }

    [Range(0, double.MaxValue)]
    public decimal OverheadCostPerBatch { get; set; }

    [Range(0, int.MaxValue)]
    public int EstimatedProductionMinutes { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }

    [StringLength(1024)]
    public string? Notes { get; set; }

    public List<CreateProductionFormulaItemDto> Items { get; set; } = new();
}

public class UpdateProductionFormulaDto : CreateProductionFormulaDto
{
}
