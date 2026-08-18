using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Localization;
using Inventory.Settings;
using Microsoft.AspNetCore.Authorization;
using Operations.Entities;
using Operations.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Settings;

namespace Operations.PurchaseOrders;

[Authorize(OperationsPermissions.PurchaseOrders.Default)]
public class PurchaseOrderAppService : OperationsAppService, IPurchaseOrderAppService
{
    private readonly IPurchaseOrderRepository _poRepository;
    private readonly IRepository<AppSupplier, Guid> _supplierRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranchInventory, Guid> _branchInventoryRepository;
    private readonly PurchaseOrderManager _manager;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ISettingProvider _settingProvider;

    public PurchaseOrderAppService(
        IPurchaseOrderRepository poRepository,
        IRepository<AppSupplier, Guid> supplierRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranchInventory, Guid> branchInventoryRepository,
        PurchaseOrderManager manager,
        BranchInventoryManager inventoryManager,
        IRepository<IdentityUser, Guid> userRepository,
        ISettingProvider settingProvider)
    {
        _poRepository = poRepository;
        _supplierRepository = supplierRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _branchInventoryRepository = branchInventoryRepository;
        _manager = manager;
        _inventoryManager = inventoryManager;
        _userRepository = userRepository;
        _settingProvider = settingProvider;
    }

    public async Task<PurchaseOrderDto> GetAsync(Guid id)
    {
        var po = await LoadWithItemsAsync(id);
        await EnsureBranchAccessAsync(po.DestBranchId);
        return await ProjectAsync(po);
    }

    public async Task<PagedResultDto<PurchaseOrderDto>> GetListAsync(GetPurchaseOrdersInput input)
    {
        var visibleBranchIds = await GetVisibleBranchIdsAsync();
        var totalCount = await _poRepository.CountFilteredAsync(
            input.Filter, input.Status, input.SupplierId, input.DestBranchId,
            input.FromDate, input.ToDate, visibleBranchIds);
        var pos = await _poRepository.GetFilteredListAsync(
            input.Filter, input.Status, input.SupplierId, input.DestBranchId,
            input.FromDate, input.ToDate, visibleBranchIds,
            input.Sorting ?? string.Empty, input.SkipCount, input.MaxResultCount);

        // 2) Resolve display names from the Inventory context + Identity in separate queries.
        var supplierIds = pos.Select(p => p.SupplierId).Distinct().ToList();
        var branchIds   = pos.Select(p => p.DestBranchId).Distinct().ToList();
        var creatorIds  = pos.Where(p => p.CreatorId.HasValue).Select(p => p.CreatorId!.Value).Distinct().ToList();

        var supplierNames = await GetSupplierNamesAsync(supplierIds);
        var branchNames   = await GetBranchNamesAsync(branchIds);
        var creatorNames  = await GetUserNamesAsync(creatorIds);
        var currency = await GetShopCurrencyAsync();

        var items = pos.Select(p => new PurchaseOrderDto
        {
            Id = p.Id,
            PONumber = p.PONumber,
            Status = p.Status,
            SupplierId = p.SupplierId,
            SupplierName = supplierNames.GetValueOrDefault(p.SupplierId, "-"),
            DestBranchId = p.DestBranchId,
            DestBranchName = branchNames.GetValueOrDefault(p.DestBranchId, "-"),
            OrderDate = p.OrderDate,
            ExpectedDeliveryDate = p.ExpectedDeliveryDate,
            ActualDeliveryDate = p.ActualDeliveryDate,
            TotalAmount = p.TotalAmount,
            Currency = currency,
            Notes = p.Notes,
            CreationTime = p.CreationTime,
            CreatorId = p.CreatorId,
            CreatorUserName = p.CreatorId.HasValue && creatorNames.TryGetValue(p.CreatorId.Value, out var n) ? n : null,
            ItemCount = p.Items.Count
        }).ToList();

        return new PagedResultDto<PurchaseOrderDto>(totalCount, items);
    }

    /// <summary>
    /// Loads a PO with its Items eagerly included. The default GetAsync does NOT include
    /// child collections, which breaks the rich-domain methods (Submit, RecordReceipt, etc.)
    /// that operate on the in-memory _items field.
    /// </summary>
    private async Task<AppPurchaseOrder> LoadWithItemsAsync(Guid id)
        => await _poRepository.GetWithItemsAsync(id);

    private async Task<Dictionary<Guid, string>> GetSupplierNamesAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var q = await _supplierRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(q.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
        return rows.ToDictionary(r => r.Id, r => r.Name);
    }

    private async Task<Dictionary<Guid, string>> GetBranchNamesAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var q = await _branchRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(q.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.NameAr, x.NameEn }));
        return rows.ToDictionary(r => r.Id, r => LocalizedBusinessText.Select(r.NameAr, r.NameEn));
    }

    [Authorize(OperationsPermissions.PurchaseOrders.Create)]
    public async Task<PurchaseOrderDto> CreateAsync(CreatePurchaseOrderDto input)
    {
        await _supplierRepository.GetAsync(input.SupplierId);
        await _branchRepository.GetAsync(input.DestBranchId);
        await EnsureBranchAccessAsync(input.DestBranchId);

        var currency = await GetShopCurrencyAsync();
        var po = await _manager.CreateDraftAsync(
            input.SupplierId,
            input.DestBranchId,
            input.OrderDate,
            input.ExpectedDeliveryDate,
            currency,
            input.Notes);

        await _poRepository.InsertAsync(po, autoSave: true);
        return await ProjectAsync(po);
    }

    [Authorize(OperationsPermissions.PurchaseOrders.Edit)]
    public async Task<PurchaseOrderDto> UpdateHeaderAsync(Guid id, UpdatePurchaseOrderHeaderDto input)
    {
        await _supplierRepository.GetAsync(input.SupplierId);
        await _branchRepository.GetAsync(input.DestBranchId);

        var po = await LoadWithItemsAsync(id);
        // Both sides of a branch change must be managed by the caller — the current
        // branch (it's their PO) and the new one (they'd be redirecting stock into it).
        await EnsureBranchAccessAsync(po.DestBranchId);
        if (input.DestBranchId != po.DestBranchId)
        {
            await EnsureBranchAccessAsync(input.DestBranchId);
        }
        po.EditHeader(input.SupplierId, input.DestBranchId, input.OrderDate, input.ExpectedDeliveryDate, input.Notes);
        await _poRepository.UpdateAsync(po, autoSave: true);
        return await ProjectAsync(po);
    }

    [Authorize(OperationsPermissions.PurchaseOrders.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var po = await LoadWithItemsAsync(id);
        await EnsureBranchAccessAsync(po.DestBranchId);
        if (po.Status != PurchaseOrderStatuses.Draft && po.Status != PurchaseOrderStatuses.Cancelled)
        {
            throw new BusinessException(OperationsErrorCodes.CannotModifyAfterDraft)
                .WithData("CurrentStatus", po.Status);
        }
        await _poRepository.DeleteAsync(id);
    }

    [Authorize(OperationsPermissions.PurchaseOrders.Edit)]
    public async Task<PurchaseOrderItemDto> AddItemAsync(Guid id, AddPurchaseOrderItemDto input)
    {
        await _productRepository.GetAsync(input.ProductId);
        var po = await LoadWithItemsAsync(id);
        await EnsureBranchAccessAsync(po.DestBranchId);
        var item = po.AddItem(GuidGenerator.Create(), input.ProductId, input.OrderedQuantity, input.UnitPrice);
        await _poRepository.UpdateAsync(po, autoSave: true);
        return await ProjectItemAsync(item);
    }

    [Authorize(OperationsPermissions.PurchaseOrders.Edit)]
    public async Task<PurchaseOrderItemDto> UpdateItemAsync(Guid id, Guid itemId, UpdatePurchaseOrderItemDto input)
    {
        var po = await LoadWithItemsAsync(id);
        await EnsureBranchAccessAsync(po.DestBranchId);
        po.UpdateItem(itemId, input.OrderedQuantity, input.UnitPrice);
        await _poRepository.UpdateAsync(po, autoSave: true);
        var item = po.Items.First(i => i.Id == itemId);
        return await ProjectItemAsync(item);
    }

    [Authorize(OperationsPermissions.PurchaseOrders.Edit)]
    public async Task RemoveItemAsync(Guid id, Guid itemId)
    {
        var po = await LoadWithItemsAsync(id);
        await EnsureBranchAccessAsync(po.DestBranchId);
        po.RemoveItem(itemId);
        await _poRepository.UpdateAsync(po, autoSave: true);
    }

    [Authorize(OperationsPermissions.PurchaseOrders.Submit)]
    public async Task<PurchaseOrderDto> SubmitAsync(Guid id)
    {
        var po = await LoadWithItemsAsync(id);
        await EnsureBranchAccessAsync(po.DestBranchId);
        po.Submit();
        await _poRepository.UpdateAsync(po, autoSave: true);
        return await ProjectAsync(po);
    }

    [Authorize(OperationsPermissions.PurchaseOrders.Approve)]
    public async Task<PurchaseOrderDto> ApproveAsync(Guid id)
    {
        var po = await LoadWithItemsAsync(id);
        await EnsureBranchAccessAsync(po.DestBranchId);
        po.Approve();
        await _poRepository.UpdateAsync(po, autoSave: true);
        return await ProjectAsync(po);
    }

    [Authorize(OperationsPermissions.PurchaseOrders.Cancel)]
    public async Task<PurchaseOrderDto> CancelAsync(Guid id)
    {
        var po = await LoadWithItemsAsync(id);
        await EnsureBranchAccessAsync(po.DestBranchId);
        po.Cancel();
        await _poRepository.UpdateAsync(po, autoSave: true);
        return await ProjectAsync(po);
    }

    [Authorize(OperationsPermissions.PurchaseOrders.Receive)]
    public async Task<PurchaseOrderDto> ReceiveAsync(Guid id, ReceiveItemsDto input)
    {
        var po = await LoadWithItemsAsync(id);
        await EnsureBranchAccessAsync(po.DestBranchId);
        var receiveLines = input.Lines.Where(l => l.ReceivedQuantity > 0).ToList();
        var inputByItem = new Dictionary<Guid, ReceiveLineDto>();
        foreach (var line in receiveLines)
        {
            inputByItem[line.ItemId] = line;
        }

        var productIds = po.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id));
        var productById = products.ToDictionary(p => p.Id);
        var itemById = po.Items.ToDictionary(i => i.Id);
        var today = Clock.Now.ToUniversalTime().Date;

        foreach (var line in receiveLines)
        {
            if (!itemById.TryGetValue(line.ItemId, out var item))
            {
                throw new BusinessException(OperationsErrorCodes.PurchaseOrderItemNotFound)
                    .WithData("ItemId", line.ItemId);
            }

            var product = productById[item.ProductId];
            _manager.EnsureReceiptExpiryIsUsable(
                product.ShelfLifeDays.HasValue,
                product.Id,
                product.DisplayName,
                line.ExpiryDate,
                today);
        }

        var receipts = receiveLines.Select(l => (l.ItemId, l.ReceivedQuantity));

        var applied = po.RecordReceipt(receipts);

        foreach (var line in applied)
        {
            var inv = await _branchInventoryRepository.FirstOrDefaultAsync(
                x => x.BranchId == po.DestBranchId && x.ProductId == line.ProductId);

            if (inv == null)
            {
                inv = await _inventoryManager.InitializeAsync(po.DestBranchId, line.ProductId);
                await _branchInventoryRepository.InsertAsync(inv);
            }

            await _inventoryManager.AdjustStockAsync(
                inv,
                inv.QuantityOnHand + line.Delta,
                StockMovementTypes.Purchase,
                notes: $"PO {po.PONumber}",
                referenceId: po.Id,
                referenceType: nameof(AppPurchaseOrder),
                batchExpiryDate: inputByItem[line.ItemId].ExpiryDate?.Date,
                batchUnitCost: line.UnitPrice);

            await _branchInventoryRepository.UpdateAsync(inv);
        }

        await _poRepository.UpdateAsync(po, autoSave: true);
        return await ProjectAsync(po);
    }

    // ─── Projection helpers ───

    private async Task<PurchaseOrderDto> ProjectAsync(AppPurchaseOrder po)
    {
        var supplier = await _supplierRepository.GetAsync(po.SupplierId);
        var branch = await _branchRepository.GetAsync(po.DestBranchId);

        string? creatorUserName = null;
        if (po.CreatorId.HasValue)
        {
            var user = await _userRepository.FindAsync(po.CreatorId.Value);
            creatorUserName = user?.UserName;
        }

        var productIds = po.Items.Select(i => i.ProductId).Distinct().ToList();
        var productQ = await _productRepository.GetQueryableAsync();
        var products = await AsyncExecuter.ToListAsync(productQ.Where(p => productIds.Contains(p.Id)));
        var productMap = products.ToDictionary(p => p.Id);
        var currency = await GetShopCurrencyAsync();

        return new PurchaseOrderDto
        {
            Id = po.Id,
            PONumber = po.PONumber,
            Status = po.Status,
            SupplierId = po.SupplierId,
            SupplierName = supplier.Name,
            DestBranchId = po.DestBranchId,
            DestBranchName = branch.DisplayName,
            OrderDate = po.OrderDate,
            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
            ActualDeliveryDate = po.ActualDeliveryDate,
            TotalAmount = po.TotalAmount,
            Currency = currency,
            Notes = po.Notes,
            CreationTime = po.CreationTime,
            CreatorId = po.CreatorId,
            CreatorUserName = creatorUserName,
            ItemCount = po.Items.Count,
            Items = po.Items.Select(i => ProjectItem(i, productMap.GetValueOrDefault(i.ProductId))).ToList()
        };
    }

    private async Task<PurchaseOrderItemDto> ProjectItemAsync(AppPurchaseOrderItem item)
    {
        var product = await _productRepository.GetAsync(item.ProductId);
        return ProjectItem(item, product);
    }

    private static PurchaseOrderItemDto ProjectItem(AppPurchaseOrderItem item, AppProduct? product)
    {
        return new PurchaseOrderItemDto
        {
            Id = item.Id,
            ProductId = item.ProductId,
            ProductName = product?.DisplayName ?? "(deleted product)",
            ProductSKU = product?.SKU ?? "-",
            ProductUnit = product?.DisplayUnit ?? "-",
            OrderedQuantity = item.OrderedQuantity,
            ReceivedQuantity = item.ReceivedQuantity,
            UnitPrice = item.UnitPrice,
            Subtotal = item.Subtotal,
            TracksExpiry = product?.ShelfLifeDays.HasValue == true
        };
    }

    private async Task<Dictionary<Guid, string>> GetUserNamesAsync(List<Guid> userIds)
    {
        if (userIds.Count == 0) return new Dictionary<Guid, string>();
        var q = await _userRepository.GetQueryableAsync();
        var users = await AsyncExecuter.ToListAsync(q.Where(u => userIds.Contains(u.Id)));
        return users.ToDictionary(u => u.Id, u => u.UserName);
    }

    /// <summary>
    /// Branch isolation for state-changing PO operations. A caller with
    /// PurchaseOrders.ManageAll (admin) may act on any branch's POs; otherwise the
    /// PO's destination branch must be one the caller manages. Receiving a PO writes
    /// stock into DestBranchId, so this prevents a manager receiving goods into a
    /// branch they don't run.
    /// </summary>
    private async Task EnsureBranchAccessAsync(Guid destBranchId)
    {
        if (await AuthorizationService.IsGrantedAsync(OperationsPermissions.PurchaseOrders.ManageAll))
        {
            return;
        }

        var userId = CurrentUser.Id;
        if (userId == null ||
            !await _branchRepository.AnyAsync(b => b.Id == destBranchId && b.ManagerUserId == userId))
        {
            throw new BusinessException(InventoryErrorCodes.BranchAccessDenied)
                .WithData("BranchId", destBranchId);
        }
    }

    private async Task<List<Guid>?> GetVisibleBranchIdsAsync()
    {
        if (await AuthorizationService.IsGrantedAsync(OperationsPermissions.PurchaseOrders.ManageAll))
        {
            return null;
        }

        if (CurrentUser.Id is not Guid userId)
        {
            return new List<Guid>();
        }

        var branches = await _branchRepository.GetListAsync(b => b.ManagerUserId == userId);
        return branches.ConvertAll(b => b.Id);
    }

    private async Task<string> GetShopCurrencyAsync()
        => ShopCurrencySettings.Normalize(
            await _settingProvider.GetOrNullAsync(ShopCurrencySettings.Name));
}
