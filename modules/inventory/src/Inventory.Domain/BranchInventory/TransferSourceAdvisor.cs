using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Localization;
using Volo.Abp.Domain.Services;

namespace Inventory.BranchInventory;

/// <summary>
/// Deterministic decision-support service for ranking safe stock-transfer sources.
/// It never mutates inventory and never creates a decision log.
/// </summary>
public class TransferSourceAdvisor : DomainService
{
    private readonly IBranchInventoryRepository _inventoryRepository;

    public TransferSourceAdvisor(IBranchInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public async Task<List<TransferSourceRecommendation>> RecommendAsync(
        Guid destinationBranchId,
        IReadOnlyCollection<TransferSourceRequestLine> requestLines,
        IReadOnlyCollection<Guid>? sourceBranchIdScope = null)
    {
        var requested = requestLines
            .Where(x => x.ProductId != Guid.Empty && x.RequestedQuantity > 0)
            .GroupBy(x => x.ProductId)
            .Select(g => new TransferSourceRequestLine
            {
                ProductId = g.Key,
                RequestedQuantity = g.Sum(x => x.RequestedQuantity)
            })
            .ToList();

        if (destinationBranchId == Guid.Empty || requested.Count == 0)
        {
            return new List<TransferSourceRecommendation>();
        }

        var stockRows = await _inventoryRepository.GetTransferSourceStockAsync(
            destinationBranchId,
            requested.Select(x => x.ProductId).ToList(),
            sourceBranchIdScope);

        var requestedByProduct = requested.ToDictionary(x => x.ProductId);
        var recommendations = new List<TransferSourceRecommendation>();

        foreach (var branchRows in stockRows.GroupBy(x => new
                 {
                     x.BranchId,
                     x.BranchNameAr,
                     x.BranchNameEn
                 }))
        {
            var products = new List<TransferSourceRecommendationLine>();

            foreach (var row in branchRows)
            {
                if (!requestedByProduct.TryGetValue(row.ProductId, out var request))
                {
                    continue;
                }

                var safeAvailable = Math.Max(row.QuantityOnHand - row.MinimumStock, 0);
                var covered = Math.Min(request.RequestedQuantity, safeAvailable);
                products.Add(new TransferSourceRecommendationLine
                {
                    ProductId = row.ProductId,
                    ProductName = LocalizedBusinessText.Select(row.ProductNameAr, row.ProductNameEn),
                    RequestedQuantity = request.RequestedQuantity,
                    QuantityOnHand = row.QuantityOnHand,
                    MinimumStock = row.MinimumStock,
                    SafeAvailableQuantity = safeAvailable,
                    CoveredQuantity = covered,
                    MissingQuantity = Math.Max(request.RequestedQuantity - covered, 0)
                });
            }

            var totalCovered = products.Sum(x => x.CoveredQuantity);
            if (totalCovered == 0)
            {
                continue;
            }

            var totalRequested = requested.Sum(x => x.RequestedQuantity);
            var fullyCoveredItems = products.Count(x => x.MissingQuantity == 0);
            recommendations.Add(new TransferSourceRecommendation
            {
                BranchId = branchRows.Key.BranchId,
                BranchName = LocalizedBusinessText.Select(
                    branchRows.Key.BranchNameAr,
                    branchRows.Key.BranchNameEn),
                CanFulfillAll = products.Count == requested.Count
                    && fullyCoveredItems == requested.Count,
                CoveredItemCount = fullyCoveredItems,
                TotalItemCount = requested.Count,
                CoveragePercent = totalRequested == 0
                    ? 0
                    : (int)Math.Round(totalCovered * 100m / totalRequested, MidpointRounding.AwayFromZero),
                TotalSafeAvailable = products.Sum(x => x.SafeAvailableQuantity),
                TotalMissingQuantity = products.Sum(x => x.MissingQuantity),
                Products = products
            });
        }

        recommendations = recommendations
            .OrderByDescending(x => x.CanFulfillAll)
            .ThenByDescending(x => x.CoveredItemCount)
            .ThenBy(x => x.TotalMissingQuantity)
            .ThenByDescending(x => x.TotalSafeAvailable)
            .ThenBy(x => x.BranchName)
            .ToList();

        if (recommendations.Count > 0)
        {
            recommendations[0].IsRecommended = true;
        }

        return recommendations;
    }
}
