using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Operations.Entities;
using Operations.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace Operations.Sales;

[Authorize(OperationsPermissions.Sales.Default)]
public class SaleAppService : OperationsAppService, ISaleAppService
{
    private readonly ISaleRepository _saleRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IBranchInventoryRepository _branchInventoryRepository;
    private readonly SaleManager _manager;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly IRepository<IdentityUser, Guid> _userRepository;

    public SaleAppService(
        ISaleRepository saleRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppProduct, Guid> productRepository,
        IBranchInventoryRepository branchInventoryRepository,
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
        var sale = await _saleRepository.GetWithItemsAsync(id);
        await EnsureBranchAccessAsync(sale.BranchId);
        return await ProjectAsync(sale);
    }

    public async Task<PagedResultDto<SaleDto>> GetListAsync(GetSalesInput input)
    {
        // Scope to accessible branches unless the user may manage all of them.
        IReadOnlyCollection<Guid>? branchScope = null;
        if (!await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll))
        {
            branchScope = await GetAccessibleBranchIdsAsync();
        }

        var totalCount = await _saleRepository.CountFilteredAsync(
            branchScope, input.BranchId, input.FromDate, input.ToDate, input.Filter);

        var rows = await _saleRepository.GetFilteredListAsync(
            branchScope, input.BranchId, input.FromDate, input.ToDate, input.Filter,
            input.Sorting ?? string.Empty, input.SkipCount, input.MaxResultCount);

        // Resolve denormalised display names (branch + creator live in other contexts).
        var branchIds = rows.Select(r => r.Sale.BranchId).Distinct().ToList();
        var creatorIds = new HashSet<Guid>();
        foreach (var r in rows)
        {
            if (r.Sale.CreatorId is { } creatorId) creatorIds.Add(creatorId);
        }

        var branchNames = await GetBranchNamesAsync(branchIds);
        var creatorNames = await GetUserNamesAsync(creatorIds.ToList());

        var items = rows.ConvertAll(r => new SaleDto
        {
            Id = r.Sale.Id,
            InvoiceNumber = r.Sale.InvoiceNumber,
            BranchId = r.Sale.BranchId,
            BranchName = branchNames.GetValueOrDefault(r.Sale.BranchId, "-"),
            SaleDate = r.Sale.SaleDate,
            TotalAmount = r.Sale.TotalAmount,
            Currency = r.Sale.Currency,
            Notes = r.Sale.Notes,
            CreationTime = r.Sale.CreationTime,
            CreatorId = r.Sale.CreatorId,
            CreatorUserName = r.Sale.CreatorId.HasValue && creatorNames.TryGetValue(r.Sale.CreatorId.Value, out var n) ? n : null,
            ItemCount = r.ItemCount
        });

        return new PagedResultDto<SaleDto>(totalCount, items);
    }

    public async Task<List<SaleProductLookupDto>> GetAvailableProductsAsync(Guid branchId)
    {
        await EnsureBranchAccessAsync(branchId);

        var rows = await _branchInventoryRepository.GetAvailableProductsAsync(branchId);

        return rows.ConvertAll(r => new SaleProductLookupDto
        {
            ProductId = r.Product.Id,
            Name = r.Product.Name,
            SKU = r.Product.SKU,
            Unit = r.Product.Unit,
            SalePrice = r.Product.SalePrice,
            Currency = r.Product.Currency,
            QuantityOnHand = r.Inventory.QuantityOnHand
        });
    }

    public async Task<List<Guid>> GetAccessibleBranchIdsAsync()
    {
        if (await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll))
        {
            var all = await _branchRepository.GetListAsync(b => b.IsActive);
            return all.ConvertAll(b => b.Id);
        }
        var userId = CurrentUser.Id;
        if (userId == null) return new List<Guid>();
        var mine = await _branchRepository.GetListAsync(b => b.IsActive && b.ManagerUserId == userId);
        return mine.ConvertAll(b => b.Id);
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
        var inventories = await _branchInventoryRepository.GetListAsync(
            x => x.BranchId == sale.BranchId && productIds.Contains(x.ProductId));
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
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id));
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
        var branches = await _branchRepository.GetListAsync(b => ids.Contains(b.Id));
        return branches.ToDictionary(b => b.Id, b => b.Name);
    }

    private async Task<Dictionary<Guid, string>> GetUserNamesAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var users = await _userRepository.GetListAsync(u => ids.Contains(u.Id));
        return users.ToDictionary(u => u.Id, u => u.UserName);
    }
}
