using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Operations.Entities;
using Operations.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace Operations.Sales;

[Authorize(OperationsPermissions.Sales.Default)]
public class SaleAppService : OperationsAppService, ISaleAppService
{
    private readonly IRepository<AppSale, Guid> _saleRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranchInventory, Guid> _branchInventoryRepository;
    private readonly SaleManager _manager;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly IRepository<IdentityUser, Guid> _userRepository;

    public SaleAppService(
        IRepository<AppSale, Guid> saleRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranchInventory, Guid> branchInventoryRepository,
        SaleManager manager,
        BranchInventoryManager inventoryManager,
        IRepository<IdentityUser, Guid> userRepository)
    {
        _saleRepository = saleRepository;
        _branchRepository = branchRepository;
        _productRepository = productRepository;
        _branchInventoryRepository = branchInventoryRepository;
        _manager = manager;
        _inventoryManager = inventoryManager;
        _userRepository = userRepository;
    }

    // ─── Queries ───

    public async Task<SaleDto> GetAsync(Guid id)
    {
        var sale = await LoadWithItemsAsync(id);
        await EnsureBranchAccessAsync(sale.BranchId);
        return await ProjectAsync(sale);
    }

    public async Task<PagedResultDto<SaleDto>> GetListAsync(GetSalesInput input)
    {
        var saleQ = await _saleRepository.GetQueryableAsync();

        // Scope by accessible branches if user is not ManageAll
        var hasManageAll = await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll);
        if (!hasManageAll)
        {
            var accessibleIds = await GetAccessibleBranchIdsAsync();
            var set = accessibleIds.ToHashSet();
            saleQ = saleQ.Where(s => set.Contains(s.BranchId));
        }

        if (input.BranchId.HasValue) saleQ = saleQ.Where(s => s.BranchId == input.BranchId.Value);
        if (input.FromDate.HasValue) { var f = input.FromDate.Value; saleQ = saleQ.Where(s => s.SaleDate >= f); }
        if (input.ToDate.HasValue)   { var t = input.ToDate.Value;   saleQ = saleQ.Where(s => s.SaleDate <= t); }
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            var f = input.Filter.Trim().ToLower();
            saleQ = saleQ.Where(s => s.InvoiceNumber.ToLower().Contains(f));
        }

        var totalCount = await AsyncExecuter.CountAsync(saleQ);

        var sorting = string.IsNullOrWhiteSpace(input.Sorting)
            ? $"{nameof(AppSale.SaleDate)} desc"
            : input.Sorting!;
        var sales = await AsyncExecuter.ToListAsync(saleQ.OrderBy(sorting).Skip(input.SkipCount).Take(input.MaxResultCount));

        var branchIds  = sales.Select(s => s.BranchId).Distinct().ToList();
        var creatorIds = sales.Where(s => s.CreatorId.HasValue).Select(s => s.CreatorId!.Value).Distinct().ToList();
        var branchNames  = await GetBranchNamesAsync(branchIds);
        var creatorNames = await GetUserNamesAsync(creatorIds);

        // Item counts in one round-trip
        var saleIds = sales.Select(s => s.Id).ToList();
        var itemCounts = await GetItemCountsAsync(saleIds);

        var items = sales.Select(s => new SaleDto
        {
            Id = s.Id,
            InvoiceNumber = s.InvoiceNumber,
            BranchId = s.BranchId,
            BranchName = branchNames.GetValueOrDefault(s.BranchId, "-"),
            SaleDate = s.SaleDate,
            TotalAmount = s.TotalAmount,
            Currency = s.Currency,
            Notes = s.Notes,
            CreationTime = s.CreationTime,
            CreatorId = s.CreatorId,
            CreatorUserName = s.CreatorId.HasValue && creatorNames.TryGetValue(s.CreatorId.Value, out var n) ? n : null,
            ItemCount = itemCounts.GetValueOrDefault(s.Id, 0)
        }).ToList();

        return new PagedResultDto<SaleDto>(totalCount, items);
    }

    public async Task<List<SaleProductLookupDto>> GetAvailableProductsAsync(Guid branchId)
    {
        await EnsureBranchAccessAsync(branchId);

        var invQ = await _branchInventoryRepository.GetQueryableAsync();
        var inventories = await AsyncExecuter.ToListAsync(
            invQ.Where(x => x.BranchId == branchId && x.QuantityOnHand > 0));

        if (inventories.Count == 0) return new List<SaleProductLookupDto>();

        var productIds = inventories.Select(i => i.ProductId).ToList();
        var prodQ = await _productRepository.GetQueryableAsync();
        var products = await AsyncExecuter.ToListAsync(
            prodQ.Where(p => productIds.Contains(p.Id) && p.IsActive));

        var productMap = products.ToDictionary(p => p.Id);
        return inventories
            .Where(i => productMap.ContainsKey(i.ProductId))
            .Select(i =>
            {
                var p = productMap[i.ProductId];
                return new SaleProductLookupDto
                {
                    ProductId = p.Id,
                    Name = p.Name,
                    SKU = p.SKU,
                    Unit = p.Unit,
                    SalePrice = p.SalePrice,
                    Currency = p.Currency,
                    QuantityOnHand = i.QuantityOnHand
                };
            })
            .OrderBy(x => x.Name)
            .ToList();
    }

    public async Task<List<Guid>> GetAccessibleBranchIdsAsync()
    {
        if (await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll))
        {
            var all = await _branchRepository.GetListAsync(b => b.IsActive);
            return all.Select(b => b.Id).ToList();
        }
        var userId = CurrentUser.Id;
        if (userId == null) return new List<Guid>();
        var mine = await _branchRepository.GetListAsync(b => b.IsActive && b.ManagerUserId == userId);
        return mine.Select(b => b.Id).ToList();
    }

    // ─── Atomic create ───

    [Authorize(OperationsPermissions.Sales.Manage)]
    public async Task<SaleDto> CreateAsync(CreateSaleDto input)
    {
        if (input.Items == null || input.Items.Count == 0)
        {
            throw new BusinessException(OperationsErrorCodes.CannotRecordEmptySale);
        }

        await EnsureBranchAccessAsync(input.BranchId);
        await _branchRepository.GetAsync(input.BranchId);

        // 1) Create the draft (assigns invoice number + uniqueness check).
        var sale = await _manager.CreateDraftAsync(
            input.BranchId,
            input.InvoiceNumber,
            input.SaleDate,
            input.Currency,
            input.Notes);

        // 2) Materialise items on the aggregate (each AddItem validates qty/price).
        foreach (var line in input.Items)
        {
            await _productRepository.GetAsync(line.ProductId);
            sale.AddItem(GuidGenerator.Create(), line.ProductId, line.Quantity, line.UnitPrice);
        }

        // 3) Fetch current stock for every item's product at this branch.
        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var invQ = await _branchInventoryRepository.GetQueryableAsync();
        var inventories = await AsyncExecuter.ToListAsync(
            invQ.Where(x => x.BranchId == sale.BranchId && productIds.Contains(x.ProductId)));
        var invByProduct = inventories.ToDictionary(x => x.ProductId);
        var stockSnapshot = invByProduct.ToDictionary(kv => kv.Key, kv => kv.Value.QuantityOnHand);

        // 4) Domain validates stock and raises SaleRecordedEto.
        sale.Record(stockSnapshot);

        // 5) Persist the sale so movement rows below can reference its Id with FK integrity.
        await _saleRepository.InsertAsync(sale, autoSave: true);

        // 6) Decrement stock and write a movement row per item.
        //    BranchInventoryManager.AdjustStockAsync uses MovementType "Sale" → bumps LastSoldDate.
        foreach (var item in sale.Items)
        {
            var inv = invByProduct[item.ProductId];
            await _inventoryManager.AdjustStockAsync(
                inv,
                inv.QuantityOnHand - item.Quantity,
                StockMovementTypes.Sale,
                notes: sale.InvoiceNumber,
                referenceId: sale.Id,
                referenceType: nameof(AppSale));
            await _branchInventoryRepository.UpdateAsync(inv);
        }

        return await ProjectAsync(sale);
    }

    [Authorize(OperationsPermissions.Sales.Delete)]
    public async Task DeleteAsync(Guid id)
    {
        var sale = await _saleRepository.GetAsync(id);
        // Soft delete via FullAuditedAggregateRoot; stock is not restored — admin decision.
        await _saleRepository.DeleteAsync(sale);
    }

    // ─── Helpers ───

    private async Task<AppSale> LoadWithItemsAsync(Guid id)
    {
        var query = await _saleRepository.WithDetailsAsync(s => s.Items);
        var sale = await AsyncExecuter.FirstOrDefaultAsync(query.Where(s => s.Id == id));
        if (sale == null) throw new EntityNotFoundException(typeof(AppSale), id);
        return sale;
    }

    private async Task EnsureBranchAccessAsync(Guid branchId)
    {
        if (await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll)) return;
        var userId = CurrentUser.Id;
        if (userId == null ||
            !await _branchRepository.AnyAsync(b => b.Id == branchId && b.ManagerUserId == userId))
        {
            throw new BusinessException("Operations:Sales:BranchAccessDenied")
                .WithData("BranchId", branchId);
        }
    }

    private async Task<SaleDto> ProjectAsync(AppSale sale)
    {
        var branch = await _branchRepository.GetAsync(sale.BranchId);

        string? creatorUserName = null;
        if (sale.CreatorId.HasValue)
        {
            var user = await _userRepository.FindAsync(sale.CreatorId.Value);
            creatorUserName = user?.UserName;
        }

        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var prodQ = await _productRepository.GetQueryableAsync();
        var products = await AsyncExecuter.ToListAsync(prodQ.Where(p => productIds.Contains(p.Id)));
        var productMap = products.ToDictionary(p => p.Id);

        return new SaleDto
        {
            Id = sale.Id,
            InvoiceNumber = sale.InvoiceNumber,
            BranchId = sale.BranchId,
            BranchName = branch.Name,
            SaleDate = sale.SaleDate,
            TotalAmount = sale.TotalAmount,
            Currency = sale.Currency,
            Notes = sale.Notes,
            CreationTime = sale.CreationTime,
            CreatorId = sale.CreatorId,
            CreatorUserName = creatorUserName,
            ItemCount = sale.Items.Count,
            Items = sale.Items.Select(i => ProjectItem(i, productMap.GetValueOrDefault(i.ProductId))).ToList()
        };
    }

    private static SaleItemDto ProjectItem(AppSaleItem item, AppProduct? product) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        ProductName = product?.Name ?? "(deleted product)",
        ProductSKU = product?.SKU ?? "-",
        ProductUnit = product?.Unit ?? "-",
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice,
        Subtotal = item.Subtotal
    };

    private async Task<Dictionary<Guid, string>> GetBranchNamesAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var q = await _branchRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(q.Where(x => ids.Contains(x.Id)).Select(x => new { x.Id, x.Name }));
        return rows.ToDictionary(r => r.Id, r => r.Name);
    }

    private async Task<Dictionary<Guid, string>> GetUserNamesAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var q = await _userRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(q.Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.UserName }));
        return rows.ToDictionary(r => r.Id, r => r.UserName);
    }

    private async Task<Dictionary<Guid, int>> GetItemCountsAsync(List<Guid> saleIds)
    {
        if (saleIds.Count == 0) return new Dictionary<Guid, int>();
        var q = await _saleRepository.WithDetailsAsync(s => s.Items);
        var rows = await AsyncExecuter.ToListAsync(
            q.Where(s => saleIds.Contains(s.Id)).Select(s => new { s.Id, Count = s.Items.Count }));
        return rows.ToDictionary(r => r.Id, r => r.Count);
    }
}
