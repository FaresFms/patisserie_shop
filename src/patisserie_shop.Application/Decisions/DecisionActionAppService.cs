using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Entities;
using Intelligence.Permissions;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Products;
using Inventory.StockBatches;
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
/// to target branch. WasteWriteOff → immediate WriteOff stock adjustment of the
/// currently expired quantity (no draft document — the movement ledger is the audit
/// trail). ExcessStockAlert / DeadStockFlag → no document; just executed.
/// The created document's type/id is recorded on the decision log row.
/// </summary>
[Authorize(IntelligencePermissions.DecisionLogs.Acknowledge)]
public class DecisionActionAppService : patisserie_shopAppService, IDecisionActionAppService
{
    /// <summary>Notes column limit on AppPurchaseOrder (HasMaxLength(512) in the Operations EF config).</summary>
    private const int PurchaseOrderNotesMaxLength = 512;

    /// <summary>
    /// Decision types that reorder by raising a draft purchase order to the product's
    /// default supplier — the only types eligible for consolidation.
    /// </summary>
    private static readonly string[] ReorderDecisionTypes =
    {
        DecisionTypes.LowStockAlert,
        DecisionTypes.ReorderSuggestion,
        DecisionTypes.StockoutRisk
    };

    /// <summary>
    /// Minimum received-with-date sample size before the MEASURED lead time is trusted
    /// enough to override the supplier's configured LeadTimeDays in the ROP formula.
    /// Below this the configured value wins (a couple of deliveries is too noisy).
    /// </summary>
    private const int MinLeadTimeSampleSize = 3;

    /// <summary>Trailing window for the measured-lead-time lookup (last ~6 months of PO history).</summary>
    private const int LeadTimeWindowDays = 180;

    /// <summary>
    /// Page cap when gathering pending reorder decisions to consolidate. Matches ABP's
    /// default MaxMaxResultCount so the decision-log list endpoint accepts it.
    /// </summary>
    private const int MaxConsolidationFetch = 1000;

    private readonly IDecisionLogAppService _decisionLogAppService;
    private readonly IPurchaseOrderAppService _purchaseOrderAppService;
    private readonly IStockTransferAppService _stockTransferAppService;
    private readonly IProductAppService _productAppService;
    private readonly IBranchInventoryAppService _branchInventoryAppService;
    private readonly ISupplierAppService _supplierAppService;
    private readonly IRepository<AppProductVelocity, Guid> _velocityRepository;
    private readonly IStockBatchRepository _stockBatchRepository;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;

    public DecisionActionAppService(
        IDecisionLogAppService decisionLogAppService,
        IPurchaseOrderAppService purchaseOrderAppService,
        IStockTransferAppService stockTransferAppService,
        IProductAppService productAppService,
        IBranchInventoryAppService branchInventoryAppService,
        ISupplierAppService supplierAppService,
        IRepository<AppProductVelocity, Guid> velocityRepository,
        IStockBatchRepository stockBatchRepository,
        IPurchaseOrderRepository purchaseOrderRepository)
    {
        _decisionLogAppService = decisionLogAppService;
        _purchaseOrderAppService = purchaseOrderAppService;
        _stockTransferAppService = stockTransferAppService;
        _productAppService = productAppService;
        _branchInventoryAppService = branchInventoryAppService;
        _supplierAppService = supplierAppService;
        _velocityRepository = velocityRepository;
        _stockBatchRepository = stockBatchRepository;
        _purchaseOrderRepository = purchaseOrderRepository;
    }

    /// <summary>Notes prefix marking a document the rule autopilot created (not a human).</summary>
    private const string AutopilotNotesTag = "[Auto] ";

    public async Task<DecisionActionResultDto> ExecuteDecisionAsync(Guid decisionLogId, bool createdByAutopilot = false)
    {
        var decision = await _decisionLogAppService.GetAsync(decisionLogId);

        EnsurePending(decision);

        var result = decision.DecisionType switch
        {
            DecisionTypes.LowStockAlert or DecisionTypes.ReorderSuggestion or DecisionTypes.StockoutRisk
                => await CreateDraftPurchaseOrderAsync(decision, createdByAutopilot),
            DecisionTypes.TransferSuggestion
                => await CreateDraftStockTransferAsync(decision, createdByAutopilot),
            DecisionTypes.WasteWriteOff
                => await ExecuteWasteWriteOffAsync(decision),
            _ => new DecisionActionResultDto { ActionCreated = false }
        };

        await _decisionLogAppService.ExecuteAsync(decisionLogId, new ExecuteDecisionLogInput
        {
            ActionType = result.ActionType,
            ActionId = result.ActionId
        });

        return result;
    }

    public async Task<DecisionActionPreviewDto> PrepareDecisionActionAsync(Guid decisionLogId)
    {
        var decision = await _decisionLogAppService.GetAsync(decisionLogId);
        EnsurePending(decision);

        return decision.DecisionType switch
        {
            DecisionTypes.LowStockAlert or DecisionTypes.ReorderSuggestion or DecisionTypes.StockoutRisk
                => await PreviewDraftPurchaseOrderAsync(decision),
            DecisionTypes.TransferSuggestion
                => await PreviewDraftStockTransferAsync(decision),
            DecisionTypes.WasteWriteOff
                => await PreviewWasteWriteOffAsync(decision),
            _ => new DecisionActionPreviewDto
            {
                ActionCreated = false,
                ProductName = decision.ProductName,
                BranchName = decision.BranchName,
                Notes = decision.SuggestedAction
            }
        };
    }

    private static void EnsurePending(DecisionLogDto decision)
    {
        // Fail fast before creating any document — the final ExecuteAsync would
        // reject a non-Pending log anyway, but by then the draft would exist.
        if (decision.Status != DecisionLogStatuses.Pending)
        {
            throw new BusinessException(IntelligenceErrorCodes.DecisionLogNotPending)
                .WithData("CurrentStatus", decision.Status);
        }
    }

    /// <summary>
    /// Consolidates pending reorder decisions into draft POs, one per
    /// (supplier × destination branch) group. Real purchasing batches lines onto a
    /// single order rather than cutting a PO per SKU. Within a group, decisions for the
    /// SAME product collapse to ONE line at the MAX computed quantity (no double-ordering),
    /// and all those decisions are linked to the PO. Decisions whose product has no default
    /// supplier are skipped and counted — never fatal to the batch. The result is DRAFT POs
    /// a human still approves.
    /// </summary>
    public async Task<ConsolidationResultDto> ConsolidateReordersAsync(ConsolidateReordersInput input)
    {
        var result = new ConsolidationResultDto();

        var idFilter = input.DecisionLogIds is { Length: > 0 }
            ? new HashSet<Guid>(input.DecisionLogIds)
            : null;

        // Gather pending reorder decisions across all three reorder types. The list
        // endpoint filters by a SINGLE DecisionType, so query once per type (each call
        // is already branch-scoped server-side) and merge.
        var candidates = new List<DecisionLogDto>();
        foreach (var decisionType in ReorderDecisionTypes)
        {
            var page = await _decisionLogAppService.GetListAsync(new GetDecisionLogsInput
            {
                Status = DecisionLogStatuses.Pending,
                DecisionType = decisionType,
                BranchId = input.BranchId,
                SkipCount = 0,
                MaxResultCount = MaxConsolidationFetch,
                Sorting = "CreationTime"
            });
            candidates.AddRange(page.Items);
        }

        // Group by (supplier × branch). A decision without a branch, or whose product has
        // no default supplier, can't reorder — the latter is skipped+counted, the former
        // can't occur for reorder types created by the engine but is defended against.
        var groups = new Dictionary<(Guid SupplierId, Guid BranchId), List<(DecisionLogDto Decision, ProductDto Product)>>();

        foreach (var decision in candidates)
        {
            if (idFilter != null && !idFilter.Contains(decision.Id))
            {
                continue;
            }

            if (!decision.BranchId.HasValue)
            {
                continue;
            }

            var product = await _productAppService.GetAsync(decision.ProductId);
            if (!product.DefaultSupplierId.HasValue)
            {
                result.SkippedNoSupplier++;
                continue;
            }

            var key = (product.DefaultSupplierId.Value, decision.BranchId.Value);
            if (!groups.TryGetValue(key, out var members))
            {
                members = new List<(DecisionLogDto, ProductDto)>();
                groups[key] = members;
            }

            members.Add((decision, product));
        }

        foreach (var (key, members) in groups)
        {
            var consolidated = await CreateConsolidatedPurchaseOrderAsync(key.BranchId, members);
            result.PurchaseOrders.Add(consolidated);
            result.PurchaseOrdersCreated++;
            result.DecisionsConsolidated += consolidated.DecisionCount;
        }

        return result;
    }

    /// <summary>
    /// Builds ONE draft PO for a single (supplier × branch) group: merges decisions that
    /// reference the same product into one line at the MAX computed quantity, adds a line
    /// per distinct product, then marks every member decision Executed and linked to the PO.
    /// </summary>
    private async Task<ConsolidatedPoDto> CreateConsolidatedPurchaseOrderAsync(
        Guid branchId,
        List<(DecisionLogDto Decision, ProductDto Product)> members)
    {
        // First member's product currency seeds the PO (all lines share a supplier, so
        // currency is effectively per-supplier). Compute each decision's quantity with the
        // shared helper so consolidated numbers match the single-execute path exactly.
        var supplierId = members[0].Product.DefaultSupplierId!.Value;

        // ProductId → (max quantity, unit price, all decisions referencing it).
        var lines = new Dictionary<Guid, (int Quantity, decimal UnitPrice, List<Guid> DecisionIds, ProductDto Product)>();

        foreach (var (decision, product) in members)
        {
            var (quantity, _) = await ComputeReorderLineAsync(decision, product);

            if (lines.TryGetValue(product.Id, out var existing))
            {
                existing.Quantity = Math.Max(existing.Quantity, quantity);
                existing.DecisionIds.Add(decision.Id);
                lines[product.Id] = existing;
            }
            else
            {
                lines[product.Id] = (quantity, product.CostPrice, new List<Guid> { decision.Id }, product);
            }
        }

        var po = await _purchaseOrderAppService.CreateAsync(new CreatePurchaseOrderDto
        {
            SupplierId = supplierId,
            DestBranchId = branchId,
            OrderDate = DateTime.Today,
            Currency = members[0].Product.Currency,
            Notes = TruncateNotes(L["AutoNote:Consolidated", members.Count, DateTime.Today.ToString("yyyy-MM-dd")])
        });

        foreach (var line in lines.Values)
        {
            await _purchaseOrderAppService.AddItemAsync(po.Id, new AddPurchaseOrderItemDto
            {
                ProductId = line.Product.Id,
                OrderedQuantity = line.Quantity,
                UnitPrice = line.UnitPrice
            });
        }

        var decisionCount = 0;
        foreach (var (decision, _) in members)
        {
            await _decisionLogAppService.ExecuteAsync(decision.Id, new ExecuteDecisionLogInput
            {
                ActionType = DecisionActionTypes.PurchaseOrder,
                ActionId = po.Id
            });
            decisionCount++;
        }

        return new ConsolidatedPoDto
        {
            PurchaseOrderId = po.Id,
            PONumber = po.PONumber,
            SupplierName = po.SupplierName,
            BranchName = po.DestBranchName,
            LineCount = lines.Count,
            DecisionCount = decisionCount
        };
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
    private async Task<DecisionActionResultDto> CreateDraftPurchaseOrderAsync(
        DecisionLogDto decision, bool createdByAutopilot)
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

        var branchId = decision.BranchId.Value;

        var (quantity, explanation) = await ComputeReorderLineAsync(decision, product);

        var po = await _purchaseOrderAppService.CreateAsync(new CreatePurchaseOrderDto
        {
            SupplierId = product.DefaultSupplierId.Value,
            DestBranchId = branchId,
            OrderDate = DateTime.Today,
            Currency = product.Currency,
            Notes = TruncateNotes(
                Tag(createdByAutopilot)
                + L["AutoNote:FromStockAlert", DateTime.Today.ToString("yyyy-MM-dd")] + "\n"
                + explanation)
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

    private async Task<DecisionActionPreviewDto> PreviewDraftPurchaseOrderAsync(DecisionLogDto decision)
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
        var (quantity, explanation) = await ComputeReorderLineAsync(decision, product);

        return new DecisionActionPreviewDto
        {
            ActionCreated = true,
            ActionType = DecisionActionTypes.PurchaseOrder,
            ProductName = product.Name,
            BranchName = decision.BranchName,
            SupplierName = supplier.Name,
            Quantity = quantity,
            Notes = explanation,
            ProductId = product.Id,
            BranchId = decision.BranchId,
            SupplierId = product.DefaultSupplierId,
            UnitPrice = product.CostPrice,
            Lines =
            {
                new DecisionActionPreviewLineDto
                {
                    ProductName = product.Name,
                    Quantity = quantity,
                    UnitPrice = product.CostPrice,
                    Notes = explanation
                }
            }
        };
    }

    /// <summary>
    /// Computes the order quantity (and its human-readable explanation) for a single
    /// reorder decision against a product whose default supplier is already resolved.
    /// This is the ROP/fallback math shared by single-execute and consolidation, so a
    /// consolidated line carries the EXACT same quantity it would as a one-off PO.
    /// </summary>
    private async Task<(int Quantity, string Explanation)> ComputeReorderLineAsync(
        DecisionLogDto decision,
        ProductDto product)
    {
        var branchId = decision.BranchId!.Value;
        var productId = decision.ProductId;

        var supplier = await _supplierAppService.GetAsync(product.DefaultSupplierId!.Value);

        var velocity = await _velocityRepository.FindAsync(
            v => v.ProductId == productId && v.BranchId == branchId);

        var inventory = await FindBranchInventoryAsync(branchId, productId, product.SKU);
        var currentQty = inventory?.QuantityOnHand ?? decision.StockAtEvaluation ?? 0;
        var reorderLevel = inventory?.MinimumStock ?? product.ReorderLevel;

        // Prefer the supplier's MEASURED lead time when there's enough received-with-date
        // history; otherwise the configured LeadTimeDays. The descriptor goes into the PO
        // notes so the reader knows which value drove the cover-days math.
        var (leadTimeDays, leadTimeDescriptor) = await ResolveEffectiveLeadTimeAsync(
            product.DefaultSupplierId.Value, supplier.LeadTimeDays);

        return ComputeReorderQuantity(
            velocity?.AvgDailySales30,
            leadTimeDays,
            leadTimeDescriptor,
            currentQty,
            inventory?.MaximumStock,
            reorderLevel);
    }

    /// <summary>
    /// Draft transfer from the excess (source) branch to the low (target) branch.
    /// Quantity: min(max(target deficit, half the source surplus), source surplus),
    /// floored at 1 — surplus is stock above the source's own reorder level, so the
    /// source is never drained below it.
    /// </summary>
    private async Task<DecisionActionResultDto> CreateDraftStockTransferAsync(
        DecisionLogDto decision, bool createdByAutopilot)
    {
        var plan = await ComputeStockTransferPlanAsync(decision);

        var transfer = await _stockTransferAppService.CreateAsync(new CreateStockTransferDto
        {
            FromBranchId = decision.SourceBranchId!.Value,
            ToBranchId = decision.TargetBranchId!.Value,
            RequestedDate = DateTime.Today,
            Notes = Tag(createdByAutopilot)
                    + L["AutoNote:TransferSuggested", plan.Quantity, plan.TargetDeficit, plan.SourceSurplus]
        });

        await _stockTransferAppService.AddItemAsync(transfer.Id, new AddStockTransferItemDto
        {
            ProductId = decision.ProductId,
            RequestedQuantity = plan.Quantity
        });

        return new DecisionActionResultDto
        {
            ActionCreated = true,
            ActionType = DecisionActionTypes.StockTransfer,
            ActionId = transfer.Id,
            ActionNumber = transfer.Reference
        };
    }

    private async Task<DecisionActionPreviewDto> PreviewDraftStockTransferAsync(DecisionLogDto decision)
    {
        var plan = await ComputeStockTransferPlanAsync(decision);

        return new DecisionActionPreviewDto
        {
            ActionCreated = true,
            ActionType = DecisionActionTypes.StockTransfer,
            ProductName = plan.Product.Name,
            SourceBranchName = decision.SourceBranchName,
            TargetBranchName = decision.TargetBranchName,
            Quantity = plan.Quantity,
            ProductId = plan.Product.Id,
            SourceBranchId = decision.SourceBranchId,
            TargetBranchId = decision.TargetBranchId,
            Notes = L["AutoNote:TransferSuggested", plan.Quantity, plan.TargetDeficit, plan.SourceSurplus],
            Lines =
            {
                new DecisionActionPreviewLineDto
                {
                    ProductName = plan.Product.Name,
                    Quantity = plan.Quantity
                }
            }
        };
    }

    private async Task<(ProductDto Product, int Quantity, int TargetDeficit, int SourceSurplus)> ComputeStockTransferPlanAsync(
        DecisionLogDto decision)
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

        return (product, quantity, targetDeficit, sourceSurplus);
    }

    /// <summary>
    /// Executes a WasteWriteOff decision: re-reads the CURRENTLY expired quantity
    /// from the batch ledger (it may have shrunk since the scanner ran — sales and
    /// transfers consume expired batches too), then decrements the stock through the
    /// public adjust path (movement type WriteOff), which records the immutable
    /// AppStockMovement and clears expired batches first via the FEFO hook.
    /// No draft document is created — ActionType is StockAdjustment with a null
    /// ActionId; the movement ledger is the audit trail.
    ///
    /// HUMAN-ONLY BY DESIGN: this arm is destructive (stock leaves the system), so
    /// the rule autopilot never routes WasteWriteOff decisions here — see
    /// <see cref="DecisionAutopilotHandler"/>.
    /// </summary>
    private async Task<DecisionActionResultDto> ExecuteWasteWriteOffAsync(DecisionLogDto decision)
    {
        if (!decision.BranchId.HasValue)
        {
            throw new BusinessException(IntelligenceErrorCodes.WasteWriteOffBranchRequired)
                .WithData("DecisionId", decision.Id);
        }

        var branchId = decision.BranchId.Value;
        var product = await _productAppService.GetAsync(decision.ProductId);

        var expiredQty = await _stockBatchRepository.GetExpiredQuantityAsync(
            branchId, decision.ProductId, DateTime.UtcNow);
        if (expiredQty <= 0)
        {
            // Nothing expired remains (sold/transferred/already written off since the
            // scan) — mark executed with no corrective action, like ExcessStockAlert.
            return new DecisionActionResultDto { ActionCreated = false };
        }

        var inventory = await FindBranchInventoryAsync(branchId, decision.ProductId, product.SKU)
            ?? throw new BusinessException(IntelligenceErrorCodes.WasteWriteOffNoInventory)
                .WithData("ProductName", product.Name);

        // The batch ledger is best-effort, so never write off more than is actually
        // on hand (ledger drift must not drive the authoritative stock negative).
        var writeOffQty = Math.Min(expiredQty, inventory.QuantityOnHand);
        if (writeOffQty <= 0)
        {
            return new DecisionActionResultDto { ActionCreated = false };
        }

        await _branchInventoryAppService.AdjustStockAsync(inventory.Id, new AdjustStockDto
        {
            NewQuantity = inventory.QuantityOnHand - writeOffQty,
            MovementType = StockMovementTypes.WriteOff,
            Notes = L["AutoNote:WasteWriteOff", writeOffQty],
            ConcurrencyStamp = inventory.ConcurrencyStamp
        });

        return new DecisionActionResultDto
        {
            ActionCreated = true,
            ActionType = DecisionActionTypes.StockAdjustment,
            ActionId = null, // no document — the AppStockMovement ledger is the trail
            ActionNumber = $"WriteOff −{writeOffQty}"
        };
    }

    private async Task<DecisionActionPreviewDto> PreviewWasteWriteOffAsync(DecisionLogDto decision)
    {
        if (!decision.BranchId.HasValue)
        {
            throw new BusinessException(IntelligenceErrorCodes.WasteWriteOffBranchRequired)
                .WithData("DecisionId", decision.Id);
        }

        var branchId = decision.BranchId.Value;
        var product = await _productAppService.GetAsync(decision.ProductId);

        var expiredQty = await _stockBatchRepository.GetExpiredQuantityAsync(
            branchId, decision.ProductId, DateTime.UtcNow);
        if (expiredQty <= 0)
        {
            return new DecisionActionPreviewDto
            {
                ActionCreated = false,
                ActionType = DecisionActionTypes.StockAdjustment,
                ProductName = product.Name,
                BranchName = decision.BranchName,
                Notes = L["WriteOffPreview:NothingExpired"]
            };
        }

        var inventory = await FindBranchInventoryAsync(branchId, decision.ProductId, product.SKU)
            ?? throw new BusinessException(IntelligenceErrorCodes.WasteWriteOffNoInventory)
                .WithData("ProductName", product.Name);

        var writeOffQty = Math.Min(expiredQty, inventory.QuantityOnHand);
        if (writeOffQty <= 0)
        {
            return new DecisionActionPreviewDto
            {
                ActionCreated = false,
                ActionType = DecisionActionTypes.StockAdjustment,
                ProductName = product.Name,
                BranchName = decision.BranchName,
                Notes = L["WriteOffPreview:NoStock"]
            };
        }

        return new DecisionActionPreviewDto
        {
            ActionCreated = true,
            ActionType = DecisionActionTypes.StockAdjustment,
            ProductName = product.Name,
            BranchName = decision.BranchName,
            Quantity = writeOffQty,
            Notes = L["WriteOffPreview:Confirm", writeOffQty],
            Lines =
            {
                new DecisionActionPreviewLineDto
                {
                    ProductName = product.Name,
                    Quantity = writeOffQty
                }
            }
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
    /// Resolves the lead time that drives the ROP cover-days math for a supplier.
    /// Pulls the supplier's delivery history over the last <see cref="LeadTimeWindowDays"/>
    /// days; when at least <see cref="MinLeadTimeSampleSize"/> received orders carry a
    /// delivery date, the measured average lead time (rounded) overrides the configured
    /// value. Otherwise the configured LeadTimeDays is kept. Returns the effective whole
    /// days plus a descriptor for the PO notes ("measured 4.2d lead from 6 orders" vs
    /// "configured 3d lead").
    /// </summary>
    private async Task<(int LeadTimeDays, string Descriptor)> ResolveEffectiveLeadTimeAsync(
        Guid supplierId,
        int configuredLeadTimeDays)
    {
        var toUtc = DateTime.UtcNow.Date.AddDays(1);
        var fromUtc = toUtc.AddDays(-LeadTimeWindowDays);

        var rows = await _purchaseOrderRepository.GetSupplierScorecardsAsync(
            fromUtc, toUtc, new[] { supplierId });

        var row = rows.Count > 0 ? rows[0] : null;

        if (row?.AvgActualLeadTimeDays is double measured
            && row.LeadTimeSampleSize >= MinLeadTimeSampleSize)
        {
            var measuredDays = Math.Max((int)Math.Round(measured, MidpointRounding.AwayFromZero), 0);
            return (measuredDays,
                L["ReorderWhy:LeadMeasured", row.LeadTimeSampleSize, measured.ToString("0.#")]);
        }

        return (configuredLeadTimeDays, L["ReorderWhy:LeadConfigured", configuredLeadTimeDays]);
    }

    /// <summary>
    /// Reorder-point math (pure arithmetic, no I/O). With demand velocity:
    /// coverDays = leadTimeDays + 7; safety = ceil(avgDaily30 × 2);
    /// target = ceil(avgDaily30 × coverDays) + safety; order = max(target − onHand, 1),
    /// capped at MaximumStock − onHand (still floored at 1) when a ceiling is set.
    /// Without velocity (no row or zero avg) it keeps the Phase 1 refill formula.
    /// <paramref name="leadTimeDescriptor"/> labels which lead time (measured vs
    /// configured) drove the cover days, for the human-readable PO notes.
    /// Returns the quantity plus a plain-language, conclusion-first explanation —
    /// the reader is a shop manager, not a supply-chain analyst.
    /// </summary>
    private (int Quantity, string Explanation) ComputeReorderQuantity(
        decimal? avgDailySales30,
        int leadTimeDays,
        string leadTimeDescriptor,
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

            // Conclusion first, detail after.
            var explanation =
                L["ReorderWhy:Velocity", quantity, coverDays, avg.ToString("0.##"), currentQty, targetQty]
                + "\n" + leadTimeDescriptor
                + (capped ? "\n" + L["ReorderWhy:Capped", maximumStock!.Value] : "");

            return (quantity, explanation);
        }

        // Phase 1 fallback: no velocity row or zero 30-day velocity.
        var fallbackQty = maximumStock != null
            ? maximumStock.Value - currentQty
            : Math.Max(reorderLevel * 2 - currentQty, reorderLevel);
        fallbackQty = Math.Max(fallbackQty, 1);

        var fallbackExplanation = maximumStock != null
            ? L["ReorderWhy:FallbackToMax", fallbackQty, currentQty, maximumStock.Value].Value
            : L["ReorderWhy:FallbackToReorderLevel", fallbackQty, currentQty, reorderLevel].Value;

        return (fallbackQty, fallbackExplanation);
    }

    /// <summary>Autopilot marker for document notes; empty for human-initiated actions.</summary>
    private static string Tag(bool createdByAutopilot) => createdByAutopilot ? AutopilotNotesTag : string.Empty;

    /// <summary>Keeps auto-generated notes inside the PO Notes column limit.</summary>
    private static string TruncateNotes(string notes)
        => notes.Length <= PurchaseOrderNotesMaxLength
            ? notes
            : notes[..PurchaseOrderNotesMaxLength];
}
