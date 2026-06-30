using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Permissions;
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

    public StockTransferAppService(
        IStockTransferRepository transferRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IBranchInventoryRepository branchInventoryRepository,
        StockTransferManager manager,
        BranchInventoryManager inventoryManager,
        IRepository<IdentityUser, Guid> userRepository)
    {
        _transferRepository = transferRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _branchInventoryRepository = branchInventoryRepository;
        _manager = manager;
        _inventoryManager = inventoryManager;
        _userRepository = userRepository;
    }

    // ─── Queries ───

    public async Task<StockTransferDto> GetAsync(Guid id)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        return await ProjectAsync(transfer);
    }

    public async Task<PagedResultDto<StockTransferDto>> GetListAsync(GetStockTransfersInput input)
    {
        var totalCount = await _transferRepository.CountFilteredAsync(
            input.Status, input.FromBranchId, input.ToBranchId, input.Filter);

        var rows = await _transferRepository.GetFilteredListAsync(
            input.Status, input.FromBranchId, input.ToBranchId, input.Filter,
            input.Sorting ?? string.Empty, input.SkipCount, input.MaxResultCount);

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
            CompletedDate = r.Transfer.CompletedDate,
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

            return requestableRows.ConvertAll(r => new StockTransferProductLookupDto
            {
                ProductId = r.Product.Id,
                Name = r.Product.Name,
                SKU = r.Product.SKU,
                Unit = r.Product.Unit,
                QuantityOnHand = r.Inventory.QuantityOnHand
            });
        }

        var rows = await _branchInventoryRepository.GetAvailableProductsAsync(transfer.FromBranchId.Value);

        return rows.ConvertAll(r => new StockTransferProductLookupDto
        {
            ProductId = r.Product.Id,
            Name = r.Product.Name,
            SKU = r.Product.SKU,
            Unit = r.Product.Unit,
            QuantityOnHand = r.Inventory.QuantityOnHand
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

        if (await IsManageAllBranchesAsync())
        {
            if (input.FromBranchId.HasValue)
            {
                await AuthorizationService.CheckAsync(OperationsPermissions.Transfers.ChooseBranches);
            }
        }
        else
        {
            if (input.FromBranchId.HasValue)
            {
                throw new BusinessException(InventoryErrorCodes.BranchAccessDenied)
                    .WithData("BranchId", input.FromBranchId.Value);
            }

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

    [Authorize(OperationsPermissions.Transfers.Ship)]
    public async Task<StockTransferDto> ShipAsync(Guid id)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        var sourceBranchId = EnsureSourceAssigned(transfer);
        await EnsureManagedBranchAsync(sourceBranchId);
        await EnsureSourceStockAvailableAsync(transfer, sourceBranchId);
        transfer.Ship();
        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        return await ProjectAsync(transfer);
    }

    [Authorize(OperationsPermissions.Transfers.Cancel)]
    public async Task<StockTransferDto> CancelAsync(Guid id)
    {
        var transfer = await _transferRepository.GetWithItemsAsync(id);
        transfer.Cancel();
        await _transferRepository.UpdateAsync(transfer, autoSave: true);
        return await ProjectAsync(transfer);
    }

    /// <summary>
    /// Completes an InTransit transfer in one Unit of Work: validates source stock,
    /// records transferred quantities (domain raises TransferCompletedEto), then
    /// decrements the source branch (TransferOut) and increments the destination
    /// branch (TransferIn) — both via BranchInventoryManager so each fires
    /// StockChangedEto and writes an immutable AppStockMovement row.
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

        var sourceByProduct = await EnsureSourceStockAvailableAsync(transfer, sourceBranchId, transferredByItem);

        // Domain records transferred quantities, flips status, raises TransferCompletedEto.
        var lines = transfer.Complete(transferredByItem);

        var reference = BuildReference(transfer.Id);
        foreach (var line in lines)
        {
            // Source branch — decrement (TransferOut).
            var srcInv = sourceByProduct[line.ProductId];
            var sourceAdjustment = await _inventoryManager.AdjustStockDetailedAsync(
                srcInv,
                srcInv.QuantityOnHand - line.Quantity,
                StockMovementTypes.TransferOut,
                notes: reference,
                referenceId: transfer.Id,
                referenceType: TransferReferenceType);
            await _branchInventoryRepository.UpdateAsync(srcInv);

            // Destination branch — load or create, then increment (TransferIn).
            var dstInv = await _branchInventoryRepository.FirstOrDefaultAsync(
                x => x.BranchId == transfer.ToBranchId && x.ProductId == line.ProductId);
            if (dstInv == null)
            {
                dstInv = await _inventoryManager.InitializeAsync(transfer.ToBranchId, line.ProductId);
                await _branchInventoryRepository.InsertAsync(dstInv);
            }

            var transferredBatchQuantity = 0;
            foreach (var batchLine in sourceAdjustment.ConsumedBatches
                         .GroupBy(x => x.ExpiryDate.Date)
                         .Select(g => new { ExpiryDate = g.Key, Quantity = g.Sum(x => x.Quantity) }))
            {
                transferredBatchQuantity += batchLine.Quantity;
                await _inventoryManager.AdjustStockAsync(
                    dstInv,
                    dstInv.QuantityOnHand + batchLine.Quantity,
                    StockMovementTypes.TransferIn,
                    notes: reference,
                    referenceId: transfer.Id,
                    referenceType: TransferReferenceType,
                    batchExpiryDate: batchLine.ExpiryDate);
            }

            var untrackedQuantity = line.Quantity - transferredBatchQuantity;
            if (untrackedQuantity > 0)
            {
                await _inventoryManager.AdjustStockAsync(
                    dstInv,
                    dstInv.QuantityOnHand + untrackedQuantity,
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
            CompletedDate = transfer.CompletedDate,
            RequestedByUserId = transfer.RequestedByUserId,
            RequestedByUserName = requestedBy,
            ApprovedByUserId = transfer.ApprovedByUserId,
            ApprovedByUserName = approvedBy,
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

    private async Task EnsureCanEditRequestAsync(AppStockTransfer transfer)
    {
        if (await IsManageAllBranchesAsync())
        {
            return;
        }

        await EnsureManagedBranchAsync(transfer.ToBranchId);
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

        foreach (var item in transfer.Items)
        {
            var qty = quantitiesByItem != null && quantitiesByItem.TryGetValue(item.Id, out var supplied)
                ? supplied
                : (item.ApprovedQuantity ?? item.RequestedQuantity);
            if (qty <= 0)
            {
                continue;
            }

            var available = sourceByProduct.TryGetValue(item.ProductId, out var inventory)
                ? inventory.QuantityOnHand
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
