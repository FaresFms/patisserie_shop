using System;
using System.Threading.Tasks;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Permissions;
using Inventory.BranchInventory;
using Inventory.Products;
using Microsoft.AspNetCore.Authorization;
using Operations.PurchaseOrders;
using Operations.StockTransfers;
using Volo.Abp;

namespace patisserie_shop.Decisions;

/// <summary>
/// Closes the decision→action loop. A thin host-level orchestrator: it only calls
/// other app services (which enforce their own Operations/Inventory permissions)
/// and entity-free arithmetic — no LINQ, no repositories, no entity writes.
///
/// LowStockAlert / ReorderSuggestion → DRAFT purchase order to the product's
/// default supplier. TransferSuggestion → DRAFT stock transfer from source to
/// target branch. ExcessStockAlert / DeadStockFlag → no document; just executed.
/// The created document's type/id is recorded on the decision log row.
/// </summary>
[Authorize(IntelligencePermissions.DecisionLogs.Acknowledge)]
public class DecisionActionAppService : patisserie_shopAppService, IDecisionActionAppService
{
    private readonly IDecisionLogAppService _decisionLogAppService;
    private readonly IPurchaseOrderAppService _purchaseOrderAppService;
    private readonly IStockTransferAppService _stockTransferAppService;
    private readonly IProductAppService _productAppService;
    private readonly IBranchInventoryAppService _branchInventoryAppService;

    public DecisionActionAppService(
        IDecisionLogAppService decisionLogAppService,
        IPurchaseOrderAppService purchaseOrderAppService,
        IStockTransferAppService stockTransferAppService,
        IProductAppService productAppService,
        IBranchInventoryAppService branchInventoryAppService)
    {
        _decisionLogAppService = decisionLogAppService;
        _purchaseOrderAppService = purchaseOrderAppService;
        _stockTransferAppService = stockTransferAppService;
        _productAppService = productAppService;
        _branchInventoryAppService = branchInventoryAppService;
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
            DecisionTypes.LowStockAlert or DecisionTypes.ReorderSuggestion
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
    /// Refill quantity: MaximumStock - current when a ceiling is set, otherwise
    /// max(reorderLevel * 2 - current, reorderLevel); always at least 1.
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

        var inventory = await FindBranchInventoryAsync(decision.BranchId.Value, decision.ProductId, product.SKU);
        var currentQty = inventory?.QuantityOnHand ?? decision.StockAtEvaluation ?? 0;
        var reorderLevel = inventory?.MinimumStock ?? product.ReorderLevel;

        var quantity = inventory?.MaximumStock != null
            ? inventory.MaximumStock.Value - currentQty
            : Math.Max(reorderLevel * 2 - currentQty, reorderLevel);
        quantity = Math.Max(quantity, 1);

        var po = await _purchaseOrderAppService.CreateAsync(new CreatePurchaseOrderDto
        {
            SupplierId = product.DefaultSupplierId.Value,
            DestBranchId = decision.BranchId.Value,
            OrderDate = DateTime.Today,
            Currency = product.Currency,
            Notes = $"Auto-created from decision {decision.Id}. " +
                    $"Refill qty {quantity} (on hand {currentQty}, " +
                    (inventory?.MaximumStock != null
                        ? $"target max {inventory.MaximumStock.Value})."
                        : $"reorder level {reorderLevel}).")
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
}
