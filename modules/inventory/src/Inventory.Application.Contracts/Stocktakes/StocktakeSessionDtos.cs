using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Inventory.BranchInventory;
using Volo.Abp.Application.Dtos;

namespace Inventory.Stocktakes;

public class StocktakeSessionLineDto : EntityDto<Guid>
{
    public Guid InventoryId { get; set; }
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSku { get; set; } = string.Empty;
    public string ProductUnit { get; set; } = string.Empty;
    public int ExpectedQuantity { get; set; }
    public bool IsPerishable { get; set; }
    public int? CountedQuantity { get; set; }
    public int? Difference { get; set; }
    public string? Reason { get; set; }
    public string? ReasonNotes { get; set; }
    public DateTime? ProductionDate { get; set; }
}

public class StocktakeSessionDto : EntityDto<Guid>
{
    public Guid BranchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SnapshotAt { get; set; }
    public Guid? StartedBy { get; set; }
    public string? StartedByName { get; set; }
    public string? Notes { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? SubmittedBy { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public Guid? ReviewedBy { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedBy { get; set; }
    public Guid? MovementReferenceId { get; set; }
    public int TotalLineCount { get; set; }
    public int CountedLineCount { get; set; }
    public int DifferenceLineCount { get; set; }
    public int AdjustedLineCount { get; set; }
    public int MatchedLineCount { get; set; }
    public int WriteOffLineCount { get; set; }
    public int ManualAdjustmentLineCount { get; set; }
    public int ShortageQuantity { get; set; }
    public int OverageQuantity { get; set; }
    public DateTime CreationTime { get; set; }
    public DateTime? LastModificationTime { get; set; }
    public string ConcurrencyStamp { get; set; } = string.Empty;
    public List<StocktakeSessionLineDto> Lines { get; set; } = new();
}

public class StocktakeSessionListDto : EntityDto<Guid>
{
    public Guid BranchId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime SnapshotAt { get; set; }
    public string? StartedByName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public string? SubmittedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public string? ReviewNotes { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? MovementReferenceId { get; set; }
    public int TotalLineCount { get; set; }
    public int CountedLineCount { get; set; }
    public int DifferenceLineCount { get; set; }
    public int AdjustedLineCount { get; set; }
    public int WriteOffLineCount { get; set; }
    public int ManualAdjustmentLineCount { get; set; }
    public int ShortageQuantity { get; set; }
    public int OverageQuantity { get; set; }
}

public class StartStocktakeSessionDto
{
    public Guid BranchId { get; set; }
}

public class StocktakeSessionDraftLineDto
{
    public Guid LineId { get; set; }
    [Range(0, int.MaxValue)]
    public int? CountedQuantity { get; set; }
    [StringLength(32)]
    public string? Reason { get; set; }
    [StringLength(120)]
    public string? ReasonNotes { get; set; }
    public DateTime? ProductionDate { get; set; }
}

public class SaveStocktakeSessionDraftDto
{
    public Guid Id { get; set; }
    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
    [StringLength(300)]
    public string? Notes { get; set; }
    public List<StocktakeSessionDraftLineDto> Lines { get; set; } = new();
}

public class SubmitStocktakeSessionDto
{
    public Guid Id { get; set; }
    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string Notes { get; set; } = string.Empty;
    public List<StocktakeSessionDraftLineDto> Lines { get; set; } = new();
}

public class ReviewStocktakeSessionDto
{
    public Guid Id { get; set; }
    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
    [StringLength(300)]
    public string? ReviewNotes { get; set; }
}

public class RejectStocktakeSessionDto
{
    public Guid Id { get; set; }
    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
    [Required]
    [StringLength(300, MinimumLength = 1)]
    public string ReviewNotes { get; set; } = string.Empty;
}

public class CancelStocktakeSessionDto
{
    public Guid Id { get; set; }
    [Required]
    public string ConcurrencyStamp { get; set; } = string.Empty;
}

public class GetStocktakeSessionsInput : PagedAndSortedResultRequestDto
{
    public Guid BranchId { get; set; }
    [StringLength(16)]
    public string? Status { get; set; }
}

public class ApproveStocktakeSessionResultDto
{
    public StocktakeSessionDto Session { get; set; } = new();
    public StocktakeResultDto Result { get; set; } = new();
}
