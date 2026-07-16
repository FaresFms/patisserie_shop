using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Permissions;
using Inventory.StockBatches;
using Microsoft.AspNetCore.Authorization;
using Operations.Entities;
using Operations.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace Operations.StockTransfers;

[Authorize(OperationsPermissions.Transfers.Default)]
public class StockTransferAppService : OperationsAppService, IStockTransferAppService
{
    private const string TransferReferenceType = "StockTransfer";

    private readonly IStockTransferRepository _transferRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IBranchInventoryRepository _branchInventoryRepository;
    private readonly StockTransferManager _manager;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly IStockBatchRepository _stockBatchRepository;

    public StockTransferAppService(
        IStockTransferRepository transferRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IBranchInventoryRepository branchInventoryRepository,
        StockTransferManager manager,
        BranchInventoryManager inventoryManager,
        IRepository<IdentityUser, Guid> userRepository,
        IStockBatchRepository stockBatchRepository)
    {
        _transferRepository = transferRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _branchInventoryRepository = branchInventoryRepository;
        _manager = manager;
        _inventoryManager = inventoryManager;
        _userRepository = userRepository;
        _stockBatchRepository = stockBatchRepository;
    }

    // ─── Queries ───

    public async Task<StockTransferDto> GetAsync(Guid id)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        await EnsureCanViewAsync(transfer);
        return await ProjectAsync(transfer);
    }

    public async Task<PagedResultDto<StockTransferDto>> GetListAsync(GetStockTransfersInput input)
    {
        long totalCount;
        List<StockTransferListRow> rows;

        if (input.View == StockTransferViews.NeedsMyAction)
        {
            var spec = await BuildActionSpecAsync();
            totalCount = await _transferRepository.CountActionRequiredAsync(
                spec, input.Status, input.FromBranchId, input.ToBranchId, input.Filter);
            rows = await _transferRepository.GetActionRequiredListAsync(
                spec, input.Status, input.FromBranchId, input.ToBranchId, input.Filter,
                input.Sorting ?? string.Empty, input.SkipCount, input.MaxResultCount);
        }
        else
        {
            // Incoming/outgoing views scope to the user's managed branches;
            // admins (ManageAll) see everything either way.
            List<Guid>? fromIn = null, toIn = null;
            if (input.View == StockTransferViews.Incoming || input.View == StockTransferViews.Outgoing)
            {
                var managed = await GetManagedBranchIdsAsync();
                if (managed != null)
                {
                    if (input.View == StockTransferViews.Incoming) toIn = managed;
                    else fromIn = managed;
                }
            }

            var visibility = await BuildVisibilitySpecAsync();
            totalCount = await _transferRepository.CountFilteredAsync(
                input.Status, input.FromBranchId, input.ToBranchId, input.Filter, fromIn, toIn, visibility);
            rows = await _transferRepository.GetFilteredListAsync(
                input.Status, input.FromBranchId, input.ToBranchId, input.Filter,
                input.Sorting ?? string.Empty, input.SkipCount, input.MaxResultCount, fromIn, toIn, visibility);
        }

        // Resolve denormalised display names (branches + users live in other contexts).
        var branchIds = new HashSet<Guid>();
        var userIds = new HashSet<Guid>();
        foreach (var r in rows)
        {
            if (r.Transfer.FromBranchId.HasValue)
            {
                branchIds.Add(r.Transfer.FromBranchId.Value);
            }
            branchIds.Add(r.Transfer.ToBranchId);
            if (r.Transfer.RequestedByUserId is { } req) userIds.Add(req);
        }

        var branchNames = await GetBranchNamesAsync(branchIds.ToList());
        var userNames = await GetUserNamesAsync(userIds.ToList());

        var items = rows.ConvertAll(r => new StockTransferDto
        {
            Id = r.Transfer.Id,
            Reference = BuildReference(r.Transfer.Id),
            Status = r.Transfer.Status,
            FromBranchId = r.Transfer.FromBranchId,
            FromBranchName = r.Transfer.FromBranchId.HasValue
                ? branchNames.GetValueOrDefault(r.Transfer.FromBranchId.Value, "-")
                : "Waiting for admin",
            ToBranchId = r.Transfer.ToBranchId,
            ToBranchName = branchNames.GetValueOrDefault(r.Transfer.ToBranchId, "-"),
            RequestedDate = r.Transfer.RequestedDate,
            ApprovedDate = r.Transfer.ApprovedDate,
            ShippedDate = r.Transfer.ShippedDate,
            CompletedDate = r.Transfer.CompletedDate,
            ClosureReason = r.Transfer.ClosureReason,
            RequestedByUserId = r.Transfer.RequestedByUserId,
            RequestedByUserName = r.Transfer.RequestedByUserId.HasValue
                ? userNames.GetValueOrDefault(r.Transfer.RequestedByUserId.Value)
                : null,
            ApprovedByUserId = r.Transfer.ApprovedByUserId,
            Notes = r.Transfer.Notes,
            CreationTime = r.Transfer.CreationTime,
            ItemCount = r.ItemCount
        });

        return new PagedResultDto<StockTransferDto>(totalCount, items);
    }

    public async Task<List<StockTransferProductLookupDto>> GetSourceProductsAsync(Guid id)
    {
        var transfer = await _transferRepository.GetAsync(id);
        await EnsureCanViewAsync(transfer);
        if (!transfer.FromBranchId.HasValue)
        {
            var requestableRows = await _branchInventoryRepository.GetListWithProductAsync(
                transfer.ToBranchId,
                filter: null,
                onlyOutOfStock: false,
                onlyLowStock: false,
                includeInactiveProducts: false,
                sorting: "ProductName",
                skipCount: 0,
                maxResultCount: 500);
            var requestableNonExpired = await _stockBatchRepository.GetNonExpiredQuantitiesByProductAsync(
                transfer.ToBranchId, Clock.Now.ToUniversalTime().Date);

            return requestableRows.ConvertAll(r => new StockTransferProductLookupDto
            {
                ProductId = r.Product.Id,
                Name = r.Product.Name,
                SKU = r.Product.SKU,
                Unit = r.Product.Unit,
                QuantityOnHand = StockBatchManager.GetUsableQuantity(
                    r.Product, r.Inventory, requestableNonExpired)
            });
        }

        var rows = await _branchInventoryRepository.GetAvailableProductsAsync(transfer.FromBranchId.Value);
        var nonExpired = await _stockBatchRepository.GetNonExpiredQuantitiesByProductAsync(
            transfer.FromBranchId.Value, Clock.Now.ToUniversalTime().Date);

        return rows.ConvertAll(r => new StockTransferProductLookupDto
        {
            ProductId = r.Product.Id,
            Name = r.Product.Name,
            SKU = r.Product.SKU,
            Unit = r.Product.Unit,
            QuantityOnHand = StockBatchManager.GetUsableQuantity(r.Product, r.Inventory, nonExpired)
        });
    }

    // ─── Create / items ───

    [Authorize(OperationsPermissions.Transfers.Create)]
    public async Task<StockTransferDto> CreateAsync(CreateStockTransferDto input)
    {
        if (input.FromBranchId.HasValue && input.FromBranchId.Value == input.ToBranchId)
        {
            throw new BusinessException(OperationsErrorCodes.SameSourceAndDestination);
        }

        if (input.FromBranchId.HasValue)
        {
            // Push transfer (source chosen up front) — e.g. the kitchen dispatching
            // finished goods. Needs ChooseBranches; non-admins may only push from
            // a branch they manage (destination can be any branch).
            await AuthorizationService.CheckAsync(OperationsPermissions.Transfers.ChooseBranches);
            await EnsureManagedBranchAsync(input.FromBranchId.Value);
        }
        else
        {
            // Pull request (no source yet) — requester must manage the receiving branch.
            await EnsureManagedBranchAsync(input.ToBranchId);
        }

        if (input.FromBranchId.HasValue)
        {
            await _branchRepository.GetAsync(input.FromBranchId.Value);
        }
        await _branchRepository.GetAsync(input.ToBranchId);

        var transfer = _manager.CreateDraft(
            input.FromBranchId,
            input.ToBranchId,
            input.RequestedDate,
            CurrentUser.Id,
            input.Notes);

        await _transferRepository.InsertAsync(transfer, autoSave: true);
        return await ProjectAsync(transfer);
    }

    [Authorize(OperationsPermissions.Transfers.Create)]
    public async Task<StockTransferItemDto> AddItemAsync(Guid id, AddStockTransferItemDto input)
    {
        await _productRepository.GetAsync(input.ProductId);
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        await EnsureCanEditRequestAsync(transfer);
        var item = transfer.AddItem(GuidGenerator.Create(), input.ProductId, input.RequestedQuantity);
        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        return await ProjectItemAsync(item);
    }

    [Authorize(OperationsPermissions.Transfers.Create)]
    public async Task RemoveItemAsync(Guid id, Guid itemId)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        await EnsureCanEditRequestAsync(transfer);
        transfer.RemoveItem(itemId);
        await _transferRepository.UpdateAsync(transfer, autoSave: true);
    }

    [Authorize(OperationsPermissions.Transfers.Approve)]
    public async Task<StockTransferItemDto> UpdateApprovedQuantityAsync(Guid id, Guid itemId, UpdateApprovedQuantityDto input)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        transfer.ApproveItem(itemId, input.ApprovedQuantity);
        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        var item = transfer.Items.First(i => i.Id == itemId);
        return await ProjectItemAsync(item);
    }

    // ─── Status machine ───

    [Authorize(OperationsPermissions.Transfers.Create)]
    public async Task<StockTransferDto> SubmitAsync(Guid id)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        await EnsureCanEditRequestAsync(transfer);
        transfer.Submit();
        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        return await ProjectAsync(transfer);
    }

    [Authorize(OperationsPermissions.Transfers.Approve)]
    public async Task<StockTransferDto> AssignSourceAsync(Guid id, AssignStockTransferSourceDto input)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        await _branchRepository.GetAsync(input.FromBranchId);
        transfer.AssignSourceAndApprove(input.FromBranchId, CurrentUser.Id);
        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        return await ProjectAsync(transfer);
    }

    [Authorize(OperationsPermissions.Transfers.Approve)]
    public async Task<StockTransferDto> ApproveAsync(Guid id)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        transfer.Approve(CurrentUser.Id);
        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        return await ProjectAsync(transfer);
    }

    [Authorize(OperationsPermissions.Transfers.Approve)]
    public async Task<StockTransferDto> RejectAsync(Guid id, RejectStockTransferDto input)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        transfer.Reject(CurrentUser.Id, input.Reason);
        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        return await ProjectAsync(transfer);
    }

    /// <summary>
    /// Marks the transfer in transit and decrements the source branch (TransferOut)
    /// in the same unit of work — stock leaves the shelf the moment it is shipped,
    /// so any shortage surfaces here, in front of the person packing the goods.
    /// The consumed expiry batches are recorded per item and replayed on receive.
    /// </summary>
    [Authorize(OperationsPermissions.Transfers.Ship)]
    public async Task<StockTransferDto> ShipAsync(Guid id, ShipStockTransferDto input)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        var sourceBranchId = EnsureSourceAssigned(transfer);
        await EnsureManagedBranchAsync(sourceBranchId);

        // Per-item quantities the packer chose to ship (may be short). Missing → approved.
        var shippedByItem = input.Lines.ToDictionary(l => l.ItemId, l => l.ShippedQuantity);

        // Validate the source can cover exactly what's being shipped — not the approved
        // figure — so a short shipment of what's on hand is allowed.
        var sourceByProduct = await EnsureSourceStockAvailableAsync(transfer, sourceBranchId, shippedByItem);

        var lines = transfer.Ship(shippedByItem, CurrentUser.Id);
        var reference = BuildReference(transfer.Id);

        foreach (var line in lines)
        {
            var srcInv = sourceByProduct[line.ProductId];
            var adjustment = await _inventoryManager.AdjustStockDetailedAsync(
                srcInv,
                srcInv.QuantityOnHand - line.Quantity,
                StockMovementTypes.TransferOut,
                notes: reference,
                referenceId: transfer.Id,
                referenceType: TransferReferenceType);
            await _branchInventoryRepository.UpdateAsync(srcInv);

            transfer.RecordItemShippedBatches(
                line.ItemId,
                StockTransferBatchBreakdown.Format(
                    adjustment.ConsumedBatches.Select(
                        b => new StockTransferBatchBreakdown.Line(b.ExpiryDate, b.Quantity))));
        }

        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        return await ProjectAsync(transfer);
    }

    [Authorize(OperationsPermissions.Transfers.Cancel)]
    public async Task<StockTransferDto> CancelAsync(Guid id, CancelStockTransferDto input)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);

        // A requester may withdraw their own request (destination manager on pull
        // requests, source manager on push transfers); approvers and admins may
        // cancel any transfer.
        if (!await AuthorizationService.IsGrantedAsync(OperationsPermissions.Transfers.Approve))
        {
            await EnsureCanEditRequestAsync(transfer);
        }

        transfer.Cancel(CurrentUser.Id, input?.Reason);
        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        return await ProjectAsync(transfer);
    }

    public async Task<StockTransferActionSummaryDto> GetActionSummaryAsync()
    {
        var counts = await _transferRepository.GetActionCountsAsync(await BuildActionSpecAsync());
        return new StockTransferActionSummaryDto
        {
            DraftsToSubmit = counts.DraftsToSubmit,
            PendingToApprove = counts.PendingToApprove,
            ApprovedToShip = counts.ApprovedToShip,
            InTransitToReceive = counts.InTransitToReceive
        };
    }

    /// <summary>
    /// Completes an InTransit transfer in one Unit of Work: records received
    /// quantities (domain raises TransferCompletedEto) and increments the destination
    /// branch (TransferIn) via BranchInventoryManager so each item fires StockChangedEto
    /// and writes an immutable AppStockMovement row. The source branch was already
    /// decremented at ship time; expiry batches recorded then are replayed here so the
    /// received stock keeps its real expiry dates. Anything received short of the
    /// shipped quantity is transit loss — it is simply never added back.
    /// </summary>
    [Authorize(OperationsPermissions.Transfers.Complete)]
    public async Task<StockTransferDto> CompleteAsync(Guid id, CompleteStockTransferDto input)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        var sourceBranchId = EnsureSourceAssigned(transfer);
        await EnsureManagedBranchAsync(transfer.ToBranchId);

        var transferredByItem = new Dictionary<Guid, int>();
        foreach (var line in input.Lines)
        {
            transferredByItem[line.ItemId] = line.TransferredQuantity;
        }

        var breakdownByItem = new Dictionary<Guid, string?>();
        foreach (var item in transfer.Items)
        {
            breakdownByItem[item.Id] = item.ShippedBatchBreakdown;
        }

        // Transfers shipped before ship-time deduction existed (ShippedDate is null)
        // still hold their stock at the source — apply the legacy TransferOut here so
        // completing an old in-flight transfer never creates stock out of nothing.
        var legacyShipment = transfer.ShippedDate == null;
        var legacySourceByProduct = legacyShipment
            ? await EnsureSourceStockAvailableAsync(transfer, sourceBranchId, transferredByItem)
            : null;

        // Domain validates received ≤ shipped, records the quantities,
        // flips status and raises TransferCompletedEto.
        var lines = transfer.Complete(transferredByItem, CurrentUser.Id);

        var reference = BuildReference(transfer.Id);
        foreach (var line in lines)
        {
            var batches = StockTransferBatchBreakdown.Parse(breakdownByItem.GetValueOrDefault(line.ItemId));

            if (legacySourceByProduct != null)
            {
                var srcInv = legacySourceByProduct[line.ProductId];
                var sourceAdjustment = await _inventoryManager.AdjustStockDetailedAsync(
                    srcInv,
                    srcInv.QuantityOnHand - line.Quantity,
                    StockMovementTypes.TransferOut,
                    notes: reference,
                    referenceId: transfer.Id,
                    referenceType: TransferReferenceType);
                await _branchInventoryRepository.UpdateAsync(srcInv);

                batches = sourceAdjustment.ConsumedBatches
                    .Select(b => new StockTransferBatchBreakdown.Line(b.ExpiryDate, b.Quantity))
                    .ToList();
            }

            // Destination branch — load or create, then increment (TransferIn).
            var dstInv = await _branchInventoryRepository.FirstOrDefaultAsync(
                x => x.BranchId == transfer.ToBranchId && x.ProductId == line.ProductId);
            if (dstInv == null)
            {
                dstInv = await _inventoryManager.InitializeAsync(transfer.ToBranchId, line.ProductId);
                await _branchInventoryRepository.InsertAsync(dstInv);
            }

            // Replay the batches consumed at ship time, FEFO, capped at what was received.
            var remaining = line.Quantity;
            foreach (var batch in batches)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var qty = Math.Min(batch.Quantity, remaining);
                remaining -= qty;
                await _inventoryManager.AdjustStockAsync(
                    dstInv,
                    dstInv.QuantityOnHand + qty,
                    StockMovementTypes.TransferIn,
                    notes: reference,
                    referenceId: transfer.Id,
                    referenceType: TransferReferenceType,
                    batchExpiryDate: batch.ExpiryDate);
            }

            if (remaining > 0)
            {
                await _inventoryManager.AdjustStockAsync(
                    dstInv,
                    dstInv.QuantityOnHand + remaining,
                    StockMovementTypes.TransferIn,
                    notes: reference,
                    referenceId: transfer.Id,
                    referenceType: TransferReferenceType);
            }

            await _branchInventoryRepository.UpdateAsync(dstInv);
        }

        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        return await ProjectAsync(transfer);
    }

    // ─── Projection helpers ───

    private async Task<StockTransferDto> ProjectAsync(AppStockTransfer transfer)
    {
        var fromBranch = transfer.FromBranchId.HasValue
            ? await _branchRepository.FindAsync(transfer.FromBranchId.Value)
            : null;
        var toBranch = await _branchRepository.GetAsync(transfer.ToBranchId);

        var requestedBy = transfer.RequestedByUserId.HasValue
            ? (await _userRepository.FindAsync(transfer.RequestedByUserId.Value))?.UserName
            : null;
        var approvedBy = transfer.ApprovedByUserId.HasValue
            ? (await _userRepository.FindAsync(transfer.ApprovedByUserId.Value))?.UserName
            : null;
        var closedBy = transfer.ClosedByUserId.HasValue
            ? (await _userRepository.FindAsync(transfer.ClosedByUserId.Value))?.UserName
            : null;

        var productIds = transfer.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id));
        var productMap = products.ToDictionary(p => p.Id);

        return new StockTransferDto
        {
            Id = transfer.Id,
            Reference = BuildReference(transfer.Id),
            Status = transfer.Status,
            FromBranchId = transfer.FromBranchId,
            FromBranchName = fromBranch?.Name ?? "Waiting for admin",
            ToBranchId = transfer.ToBranchId,
            ToBranchName = toBranch.Name,
            RequestedDate = transfer.RequestedDate,
            ApprovedDate = transfer.ApprovedDate,
            ShippedDate = transfer.ShippedDate,
            CompletedDate = transfer.CompletedDate,
            RequestedByUserId = transfer.RequestedByUserId,
            RequestedByUserName = requestedBy,
            ApprovedByUserId = transfer.ApprovedByUserId,
            ApprovedByUserName = approvedBy,
            ClosedByUserName = closedBy,
            ClosureReason = transfer.ClosureReason,
            Notes = transfer.Notes,
            CreationTime = transfer.CreationTime,
            ItemCount = transfer.Items.Count,
            Items = transfer.Items
                .Select(i => ProjectItem(i, productMap.GetValueOrDefault(i.ProductId)))
                .ToList()
        };
    }

    private async Task<StockTransferItemDto> ProjectItemAsync(AppStockTransferItem item)
    {
        var product = await _productRepository.FindAsync(item.ProductId);
        return ProjectItem(item, product);
    }

    private static StockTransferItemDto ProjectItem(AppStockTransferItem item, AppProduct? product) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        ProductName = product?.Name ?? "(deleted product)",
        ProductSKU = product?.SKU ?? "-",
        ProductUnit = product?.Unit ?? "-",
        RequestedQuantity = item.RequestedQuantity,
        ApprovedQuantity = item.ApprovedQuantity,
        ShippedQuantity = item.ShippedQuantity,
        TransferredQuantity = item.TransferredQuantity
    };

    private async Task<Dictionary<Guid, string>> GetBranchNamesAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var branches = await _branchRepository.GetListAsync(b => ids.Contains(b.Id));
        return branches.ToDictionary(b => b.Id, b => b.Name);
    }

    private async Task<Dictionary<Guid, string>> GetUserNamesAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var users = await _userRepository.GetListAsync(u => ids.Contains(u.Id));
        return users.ToDictionary(u => u.Id, u => u.UserName);
    }

    private static string BuildReference(Guid id) => $"TR-{id.ToString("N")[..8].ToUpperInvariant()}";

    private Task<bool> IsManageAllBranchesAsync()
        => AuthorizationService.IsGrantedAsync(InventoryPermissions.BranchInventory.ManageAll);

    /// <summary>Branch ids managed by the current user, or null when they manage all branches.</summary>
    private async Task<List<Guid>?> GetManagedBranchIdsAsync()
    {
        if (await IsManageAllBranchesAsync())
        {
            return null;
        }

        var userId = CurrentUser.Id;
        if (userId == null)
        {
            return new List<Guid>();
        }

        var branches = await _branchRepository.GetListAsync(b => b.ManagerUserId == userId);
        return branches.ConvertAll(b => b.Id);
    }

    private async Task<StockTransferActionSpec> BuildActionSpecAsync()
    {
        var managed = await GetManagedBranchIdsAsync();
        return new StockTransferActionSpec
        {
            UserId = CurrentUser.Id,
            CanApprove = await AuthorizationService.IsGrantedAsync(OperationsPermissions.Transfers.Approve),
            ManageAllBranches = managed == null,
            ManagedBranchIds = managed ?? new List<Guid>()
        };
    }

    private async Task<StockTransferVisibilitySpec> BuildVisibilitySpecAsync()
    {
        var canApprove = await AuthorizationService.IsGrantedAsync(OperationsPermissions.Transfers.Approve);
        var managed = await GetManagedBranchIdsAsync();
        return new StockTransferVisibilitySpec
        {
            CanViewAll = canApprove || managed == null,
            ManagedBranchIds = managed ?? new List<Guid>()
        };
    }

    private async Task EnsureCanViewAsync(AppStockTransfer transfer)
    {
        if (await IsManageAllBranchesAsync()
            || await AuthorizationService.IsGrantedAsync(OperationsPermissions.Transfers.Approve)
            || await IsManagerOfBranchAsync(transfer.ToBranchId)
            || (transfer.FromBranchId.HasValue && await IsManagerOfBranchAsync(transfer.FromBranchId.Value)))
        {
            return;
        }

        throw new BusinessException(InventoryErrorCodes.BranchAccessDenied)
            .WithData("BranchId", transfer.ToBranchId);
    }

    private async Task EnsureCanEditRequestAsync(AppStockTransfer transfer)
    {
        if (await IsManageAllBranchesAsync())
        {
            return;
        }

        // Pull requests are edited by the receiving branch's manager; push transfers
        // (source chosen at creation) are edited by the sending branch's manager.
        if (transfer.FromBranchId is Guid fromBranchId
            && await IsManagerOfBranchAsync(fromBranchId))
        {
            return;
        }

        await EnsureManagedBranchAsync(transfer.ToBranchId);
    }

    private async Task<bool> IsManagerOfBranchAsync(Guid branchId)
    {
        var userId = CurrentUser.Id;
        return userId != null
            && await _branchRepository.AnyAsync(b => b.Id == branchId && b.ManagerUserId == userId);
    }

    private async Task EnsureManagedBranchAsync(Guid branchId)
    {
        if (await IsManageAllBranchesAsync())
        {
            return;
        }

        var userId = CurrentUser.Id;
        if (userId == null ||
            !await _branchRepository.AnyAsync(b => b.Id == branchId && b.ManagerUserId == userId))
        {
            throw new BusinessException(InventoryErrorCodes.BranchAccessDenied)
                .WithData("BranchId", branchId);
        }
    }

    private static Guid EnsureSourceAssigned(AppStockTransfer transfer)
        => transfer.FromBranchId
           ?? throw new BusinessException(OperationsErrorCodes.TransferSourceBranchRequired)
               .WithData("TransferId", transfer.Id);

    private async Task<Dictionary<Guid, AppBranchInventory>> EnsureSourceStockAvailableAsync(
        AppStockTransfer transfer,
        Guid sourceBranchId,
        IReadOnlyDictionary<Guid, int>? quantitiesByItem = null)
    {
        var productIds = transfer.Items.Select(i => i.ProductId).Distinct().ToList();
        var sourceInventories = await _branchInventoryRepository.GetListAsync(
            x => x.BranchId == sourceBranchId && productIds.Contains(x.ProductId));
        var sourceByProduct = sourceInventories.ToDictionary(x => x.ProductId);
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id));
        var productById = products.ToDictionary(p => p.Id);
        var nonExpired = await _stockBatchRepository.GetNonExpiredQuantitiesByProductAsync(
            sourceBranchId, Clock.Now.ToUniversalTime().Date);

        foreach (var item in transfer.Items)
        {
            var approved = item.ApprovedQuantity ?? item.RequestedQuantity;
            // Match the domain's clamp so validation checks the amount that will really
            // ship — a client sending more than approved can't demand more source stock.
            var qty = quantitiesByItem != null && quantitiesByItem.TryGetValue(item.Id, out var supplied)
                ? Math.Clamp(supplied, 0, approved)
                : approved;
            if (qty <= 0)
            {
                continue;
            }

            var available = sourceByProduct.TryGetValue(item.ProductId, out var inventory)
                && productById.TryGetValue(item.ProductId, out var product)
                    ? StockBatchManager.GetUsableQuantity(product, inventory, nonExpired)
                    : 0;
            if (available < qty)
            {
                throw new BusinessException(OperationsErrorCodes.InsufficientStockAtSource)
                    .WithData("ProductId", item.ProductId)
                    .WithData("Requested", qty)
                    .WithData("Available", available);
            }
        }

        return sourceByProduct;
    }
}
