using System;
using Volo.Abp.Application.Dtos;

namespace Intelligence.Velocity;

public class ProductVelocityDto : EntityDto<Guid>
{
    public Guid ProductId { get; set; }
    public Guid BranchId { get; set; }

    public decimal AvgDailySales7 { get; set; }
    public decimal AvgDailySales30 { get; set; }
    public int QuantitySold30 { get; set; }
    public decimal Revenue30 { get; set; }

    /// <summary>"A", "B" or "C" — global per-product class by 30-day revenue share.</summary>
    public string AbcClass { get; set; } = "C";

    public DateTime ComputedAtUtc { get; set; }

    /// <summary>Resolved by the repository join from the product.</summary>
    public string? ProductName { get; set; }

    /// <summary>Resolved by the repository join from the product.</summary>
    public string? ProductSku { get; set; }

    /// <summary>Resolved by the repository join from the branch.</summary>
    public string? BranchName { get; set; }

    /// <summary>QuantityOnHand of the matching branch-inventory row.</summary>
    public int CurrentStock { get; set; }

    /// <summary>CurrentStock ÷ AvgDailySales30; null when the 30-day velocity is zero.</summary>
    public decimal? DaysOfCover { get; set; }
}
