using System;
using System.Threading.Tasks;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Permissions;
using Inventory.BranchInventory;
using Inventory.Products;
using Inventory.Suppliers;
using Microsoft.AspNetCore.Authorization;
using Operations.PurchaseOrders;
using Operations.StockTransfers;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace patisserie_shop.Decisions;

/// <summary>
/// Closes the decision→action loop. A thin host-level orchestrator: it only calls
/// other app services (which enforce their own Operations/Inventory permissions)
/// and entity-free arithmetic — no LINQ, no repositories, no entity writes.
///
/// LowStockAlert / ReorderSuggestion / StockoutRisk → DRAFT purchase order to the
/// product's default supplier. TransferSuggestion → DRAFT stock transfer from source
/// to target branch. ExcessStockAlert / DeadStockFlag → no document; just executed.
/// The created document's type/id is recorded on the decision log row.
/// </summary>
[Authorize(IntelligencePermissions.DecisionLogs.Acknowledge)]
public class DecisionActionAppService : patisserie_shopAppService, IDecisionActionAppService
{
    /// <summary>Notes column limit on AppPurchaseOrder (HasMaxLength(512) in the Operations EF config).</summary>
    private const int PurchaseOrderNotesMaxLength = 512;

    private readonly IDecisionLogAppService _decisionLogAppService;
    private readonly IPurchaseOrderAppService _purchaseOrderAppService;
    private readonly IStockTransferAppService _stockTransferAppService;
    private readonly IProductAppService _productAppService;
    private readonly IBranchInventoryAppService _branchInventoryAppService;
    private readonly ISupplierAppService _supplierAppService;
    private readonly IRepository<AppProductVelocity, Guid> _velocityRepository;

    public DecisionActionAppService(
        IDecisionLogAppService decisionLogAppService,
        IPurchaseOrderAppService purchaseOrderAppService,
        IStockTransferAppService stockTransferAppService,
        IProductAppService productAppService,
        IBranchInventoryAppService branchInventoryAppService,
        ISupplierAppService supplierAppService,
        IRepository<AppProductVelocity, Guid> velocityRepository)
    {
        _decisionLogAppService = decisionLogAppService;
        _purchaseOrderAppService = purchaseOrderAppService;
        _stockTransferAppService = stockTransferAppService;
        _productAppService = productAppService;
        _branchInventoryAppService = branchInventoryAppService;
        _supplierAppService = supplierAppService;
        _velocityRepository = velocityRepository;
    }

    public async Task<DecisionActionResultDto> ExecuteDecisionAsync(Guid decisionLogId)
    {
        var decision = await _decisionLogAppService.GetAsync(decisionLogId);

        // Fail fast before creating any document — the final ExecuteAsync would
        // reject a non-Pending log anyway, but by then the draft would exist.
        if (decision.Status != DecisionLogStatuses.Pending)
        {
            throw new BusinessException(IntelligenceErrorCodes.DecisionLogNotPending)
                .WithData("CurrentStatus", decision.Status);
        }

        var result = decision.DecisionType switch
        {
            DecisionTypes.LowStockAlert or DecisionTypes.ReorderSuggestion or DecisionTypes.StockoutRisk
                => await CreateDraftPurchaseOrderAsync(decision),
            DecisionTypes.TransferSuggestion
                => await CreateDraftStockTransferAsync(decision),
            _ => new DecisionActionResultDto { ActionCreated = false }
        };

        await _decisionLogAppService.ExecuteAsync(decisionLogId, new ExecuteDecisionLogInput
        {
            ActionType = result.ActionType,
            ActionId = result.ActionId
        });

        return result;
    }

    /// <summary>
    /// Draft PO to the product's default supplier, one line for the affected product.
    /// Quantity comes from a reorder-point formula when demand-velocity data exists:
    /// target = ceil(avgDaily30 × (supplier lead time + 7 cover days)) + ceil(avgDaily30 × 2)
    /// safety stock, ordered down to max(target − on hand, 1) and capped at
    /// MaximumStock − on hand when a ceiling is set. Without velocity data it falls
    /// back to the Phase 1 refill: MaximumStock − current when a ceiling is set,
    /// otherwise max(reorderLevel × 2 − current, reorderLevel); always at least 1.
    /// </summary>
    private async Task<DecisionActionResultDto> CreateDraftPurchaseOrderAsync(DecisionLogDto decision)
    {
        if (!decision.BranchId.HasValue)
        {
            throw new BusinessException(IntelligenceErrorCodes.DecisionBranchRequired)
                .WithData("DecisionType", decision.DecisionType);
        }

        var product = await _productAppService.GetAsync(decision.ProductId);
        if (!product.DefaultSupplierId.HasValue)
        {
            throw new BusinessException(IntelligenceErrorCodes.DecisionProductHasNoDefaultSupplier)
                .WithData("ProductName", product.Name);
        }

        var supplier = await _supplierAppService.GetAsync(product.DefaultSupplierId.Value);

        var branchId = decision.BranchId.Value;
        var productId = decision.ProductId;
        var velocity = await _velocityRepository.FindAsync(
            v => v.ProductId == productId && v.BranchId == branchId);

        var inventory = await FindBranchInventoryAsync(branchId, productId, product.SKU);
        var currentQty = inventory?.QuantityOnHand ?? decision.StockAtEvaluation ?? 0;
        var reorderLevel = inventory?.MinimumStock ?? product.ReorderLevel;

        var (quantity, explanation) = ComputeReorderQuantity(
            velocity?.AvgDailySales30,
            supplier.LeadTimeDays,
            currentQty,
            inventory?.MaximumStock,
            reorderLevel);

        var po = await _purchaseOrderAppService.CreateAsync(new CreatePurchaseOrderDto
        {
            SupplierId = product.DefaultSupplierId.Value,
            DestBranchId = branchId,
            OrderDate = DateTime.Today,
            Currency = product.Currency,
            Notes = TruncateNotes($"Auto-created from decision {decision.Id}. {explanation}")
        });

        await _purchaseOrderAppService.AddItemAsync(po.Id, new AddPurchaseOrderItemDto
        {
            ProductId = decision.ProductId,
            OrderedQuantity = quantity,
            UnitPrice = product.CostPrice
        });

        return new DecisionActionResultDto
        {
            ActionCreated = true,
            ActionType = DecisionActionTypes.PurchaseOrder,
            ActionId = po.Id,
            ActionNumber = po.PONumber
        };
    }

    /// <summary>
    /// Draft transfer from the excess (source) branch to the low (target) branch.
    /// Quantity: min(max(target deficit, half the source surplus), source surplus),
    /// floored at 1 — surplus is stock above the source's own reorder level, so the
    /// source is never drained below it.
    /// </summary>
    private async Task<DecisionActionResultDto> CreateDraftStockTransferAsync(DecisionLogDto decision)
    {
        if (!decision.SourceBranchId.HasValue || !decision.TargetBranchId.HasValue)
        {
            throw new BusinessException(IntelligenceErrorCodes.DecisionTransferBranchesRequired)
                .WithData("DecisionId", decision.Id);
        }

        var product = await _productAppService.GetAsync(decision.ProductId);

        var sourceInv = await FindBranchInventoryAsync(decision.SourceBranchId.Value, decision.ProductId, product.SKU);
        var targetInv = await FindBranchInventoryAsync(decision.TargetBranchId.Value, decision.ProductId, product.SKU);

        var sourceQty = sourceInv?.QuantityOnHand ?? 0;
        var sourceMin = sourceInv?.MinimumStock ?? 0;
        var targetQty = targetInv?.QuantityOnHand ?? decision.StockAtEvaluation ?? 0;
        var targetMin = targetInv?.MinimumStock ?? product.ReorderLevel;

        var targetDeficit = Math.Max(targetMin - targetQty, 0);
        var sourceSurplus = Math.Max(sourceQty - sourceMin, 0);

        var quantity = Math.Min(Math.Max(targetDeficit, sourceSurplus / 2), sourceSurplus);
        quantity = Math.Max(quantity, 1);

        var transfer = await _stockTransferAppService.CreateAsync(new CreateStockTransferDto
        {
            FromBranchId = decision.SourceBranchId.Value,
            ToBranchId = decision.TargetBranchId.Value,
            RequestedDate = DateTime.Today,
            Notes = $"Auto-created from decision {decision.Id}. " +
                    $"Suggested qty {quantity} = min(max(target deficit {targetDeficit}, " +
                    $"half source surplus {sourceSurplus / 2}), source surplus {sourceSurplus})."
        });

        await _stockTransferAppService.AddItemAsync(transfer.Id, new AddStockTransferItemDto
        {
            ProductId = decision.ProductId,
            RequestedQuantity = quantity
        });

        return new DecisionActionResultDto
        {
            ActionCreated = true,
            ActionType = DecisionActionTypes.StockTransfer,
            ActionId = transfer.Id,
            ActionNumber = transfer.Reference
        };
    }

    /// <summary>
    /// Locates the branch-inventory row for a product via the existing list endpoint
    /// (filtered by the product's unique SKU). Returns null when the product was
    /// never initialized at that branch.
    /// </summary>
    private async Task<BranchInventoryDto?> FindBranchInventoryAsync(Guid branchId, Guid productId, string sku)
    {
        var page = await _branchInventoryAppService.GetListAsync(new GetBranchInventoryInput
        {
            BranchId = branchId,
            Filter = sku,
            IncludeInactiveProducts = true,
            SkipCount = 0,
            MaxResultCount = 100
        });

        foreach (var row in page.Items)
        {
            if (row.ProductId == productId)
            {
                return row;
            }
        }

        return null;
    }

    /// <summary>
    /// Reorder-point math (pure arithmetic, no I/O). With demand velocity:
    /// coverDays = leadTimeDays + 7; safety = ceil(avgDaily30 × 2);
    /// target = ceil(avgDaily30 × coverDays) + safety; order = max(target − onHand, 1),
    /// capped at MaximumStock − onHand (still floored at 1) when a ceiling is set.
    /// Without velocity (no row or zero avg) it keeps the Phase 1 refill formula.
    /// Returns the quantity plus a human-readable explanation for the PO notes.
    /// </summary>
    private static (int Quantity, string Explanation) ComputeReorderQuantity(
        decimal? avgDailySales30,
        int leadTimeDays,
        int currentQty,
        int? maximumStock,
        int reorderLevel)
    {
        if (avgDailySales30 is > 0)
        {
            var avg = avgDailySales30.Value;
            var coverDays = leadTimeDays + 7;
            var safety = (int)Math.Ceiling(avg * 2);
            var targetQty = (int)Math.Ceiling(avg * coverDays) + safety;

            var quantity = Math.Max(targetQty - currentQty, 1);
            var capped = false;
            if (maximumStock.HasValue && quantity > maximumStock.Value - currentQty)
            {
                quantity = Math.Max(maximumStock.Value - currentQty, 1);
                capped = true;
            }

            var explanation =
                $"ROP: {avg:0.##}/day × ({leadTimeDays}d lead + 7d cover) + {safety} safety " +
                $"= {targetQty} target − {currentQty} on hand → order {quantity}" +
                (capped ? $" (capped by max stock {maximumStock!.Value})." : ".");

            return (quantity, explanation);
        }

        // Phase 1 fallback: no velocity row or zero 30-day velocity.
        var fallbackQty = maximumStock != null
            ? maximumStock.Value - currentQty
            : Math.Max(reorderLevel * 2 - currentQty, reorderLevel);
        fallbackQty = Math.Max(fallbackQty, 1);

        var fallbackExplanation =
            $"Refill qty {fallbackQty} (on hand {currentQty}, " +
            (maximumStock != null
                ? $"target max {maximumStock.Value}; no sales velocity)."
                : $"reorder level {reorderLevel}; no sales velocity).");

        return (fallbackQty, fallbackExplanation);
    }

    /// <summary>Keeps auto-generated notes inside the PO Notes column limit.</summary>
    private static string TruncateNotes(string notes)
        => notes.Length <= PurchaseOrderNotesMaxLength
            ? notes
            : notes[..PurchaseOrderNotesMaxLength];
}
