using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Settings;
using Inventory.StockBatches;
using Microsoft.AspNetCore.Authorization;
using Operations.Cashiers;
using Operations.Entities;
using Operations.Permissions;
using Operations.Sales;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Settings;
using Volo.Abp.Users;

namespace Operations.Cashier;

/// <summary>
/// Cash-only POS backend. Thin orchestrator: it composes the existing SaleManager /
/// AppSale.Record / BranchInventoryManager.AdjustStockAsync pipeline (exactly like
/// SaleAppService) plus the cash-drawer aggregate. A cashier is gated by
/// <see cref="OperationsPermissions.Cashier.OperatePos"/> — NOT by being a branch manager,
/// so there is no manager-branch EnsureBranchAccess check on the sell path.
/// </summary>
[Authorize(OperationsPermissions.Cashier.Default)]
public class CashierAppService : OperationsAppService, ICashierAppService
{
    private const int VoidWindowMinutes = 60;

    private readonly ISaleRepository _saleRepository;
    private readonly ICashierShiftRepository _shiftRepository;
    private readonly CashierShiftManager _shiftManager;
    private readonly SaleManager _saleManager;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly IBranchInventoryRepository _branchInventoryRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<IdentityUser, Guid> _userRepository;
    private readonly ISettingProvider _settingProvider;
    private readonly IStockBatchRepository _stockBatchRepository;

    public CashierAppService(
        ISaleRepository saleRepository,
        ICashierShiftRepository shiftRepository,
        CashierShiftManager shiftManager,
        SaleManager saleManager,
        BranchInventoryManager inventoryManager,
        IBranchInventoryRepository branchInventoryRepository,
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<IdentityUser, Guid> userRepository,
        ISettingProvider settingProvider,
        IStockBatchRepository stockBatchRepository)
    {
        _saleRepository = saleRepository;
        _shiftRepository = shiftRepository;
        _shiftManager = shiftManager;
        _saleManager = saleManager;
        _inventoryManager = inventoryManager;
        _branchInventoryRepository = branchInventoryRepository;
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _userRepository = userRepository;
        _settingProvider = settingProvider;
        _stockBatchRepository = stockBatchRepository;
    }

    // ─── Shifts ───

    [Authorize(OperationsPermissions.Cashier.OperatePos)]
    public async Task<CashierShiftDto?> GetCurrentShiftAsync(Guid branchId)
    {
        EnsureBranchAllowed(branchId);

        var userId = CurrentUser.GetId();
        var shift = await _shiftRepository.FindOpenShiftAsync(branchId, userId);
        return shift == null ? null : await ProjectShiftAsync(shift);
    }

    [Authorize(OperationsPermissions.Cashier.OperatePos)]
    public async Task<CashierShiftDto> OpenShiftAsync(OpenShiftDto input)
    {
        EnsureBranchAllowed(input.BranchId);

        var userId = CurrentUser.GetId();
        await _branchRepository.GetAsync(input.BranchId);

        var existingShift = await _shiftRepository.FindOpenShiftAsync(input.BranchId, userId);
        if (existingShift is not null)
        {
            return await ProjectShiftAsync(existingShift);
        }

        AppCashierShift shift;
        try
        {
            shift = await _shiftManager.CreateOpenAsync(input.BranchId, userId, input.OpeningFloat);
        }
        catch (BusinessException ex) when (ex.Code == OperationsErrorCodes.ShiftAlreadyOpen)
        {
            existingShift = await _shiftRepository.FindOpenShiftAsync(input.BranchId, userId);
            if (existingShift is not null)
            {
                return await ProjectShiftAsync(existingShift);
            }

            throw;
        }

        await _shiftRepository.InsertAsync(shift, autoSave: true);

        return await ProjectShiftAsync(shift);
    }

    public async Task<CashierShiftDto> CloseShiftAsync(CloseShiftDto input)
    {
        var shift = await _shiftRepository.GetAsync(input.ShiftId);

        // A supervisor (ViewAllShifts) may close any drawer to reconcile a branch;
        // an ordinary cashier may only close their OWN shift at their assigned branch.
        var canManageAnyShift = await AuthorizationService.IsGrantedAsync(OperationsPermissions.Cashier.ViewAllShifts);
        if (!canManageAnyShift)
        {
            EnsureBranchAllowed(shift.BranchId);
            if (shift.CashierUserId != CurrentUser.GetId())
            {
                throw new BusinessException(OperationsErrorCodes.BranchNotAssignedToCashier)
                    .WithData("ShiftId", shift.Id);
            }
        }

        var totals = await _shiftRepository.GetShiftSalesTotalsAsync(shift.Id);
        var expectedCash = shift.OpeningFloat + totals.NonVoidedTotal;

        shift.Close(input.CountedCash, expectedCash, Clock.Now);
        await _shiftRepository.UpdateAsync(shift, autoSave: true);

        return await ProjectShiftAsync(shift, totals);
    }

    [Authorize(OperationsPermissions.Cashier.ViewAllShifts)]
    public async Task<List<CashierShiftDto>> GetShiftsAsync(GetShiftsInput input)
    {
        // Scope to accessible branches unless the caller may manage all of them.
        IReadOnlyCollection<Guid>? branchScope = null;
        if (!await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll))
        {
            branchScope = await GetAccessibleBranchIdsAsync();
        }

        var shifts = await _shiftRepository.GetListAsync(
            input.BranchId, input.OpenOnly, branchScope, input.SkipCount, input.MaxResultCount);

        var branchNames = await GetBranchNamesAsync(shifts.Select(s => s.BranchId).Distinct().ToList());
        var userNames = await GetUserNamesAsync(shifts.Select(s => s.CashierUserId).Distinct().ToList());

        var result = new List<CashierShiftDto>(shifts.Count);
        foreach (var shift in shifts)
        {
            var totals = await _shiftRepository.GetShiftSalesTotalsAsync(shift.Id);
            var dto = MapShift(shift, totals);
            dto.BranchName = branchNames.GetValueOrDefault(shift.BranchId);
            dto.CashierUserName = userNames.GetValueOrDefault(shift.CashierUserId);
            result.Add(dto);
        }

        return result;
    }

    // ─── Assigned branch (claim-driven; the POS branch source) ───

    [Authorize(OperationsPermissions.Cashier.OperatePos)]
    public async Task<CashierBranchDto?> GetMyBranchAsync()
    {
        var assigned = GetAssignedBranchId();
        if (assigned is null)
        {
            return null;
        }

        var branch = await _branchRepository.FindAsync(assigned.Value);
        if (branch is null || !branch.IsActive)
        {
            return null;
        }

        return new CashierBranchDto
        {
            Id = branch.Id,
            Name = branch.DisplayName,
            Address = branch.DisplayAddress
        };
    }

    // ─── Branch list (cashier-permitted lightweight branch data) ───

    public async Task<List<CashierBranchDto>> GetSellableBranchesAsync()
    {
        // Active branches only. Gated by the class-level Cashier.Default permission, so a
        // cashier can populate the POS branch picker without Inventory's Branches.Default.
        var branches = await _branchRepository.GetListAsync(b => b.IsActive);

        return branches
            .OrderBy(b => b.DisplayName)
            .Select(b => new CashierBranchDto
            {
                Id = b.Id,
                Name = b.DisplayName,
                Address = b.DisplayAddress
            })
            .ToList();
    }

    // ─── Product tiles (sale-safe availability; never exposes cost) ───

    [Authorize(OperationsPermissions.Cashier.OperatePos)]
    public async Task<List<CashierProductDto>> GetProductTilesAsync(Guid branchId, string? filter)
    {
        EnsureBranchAllowed(branchId);

        // Typed read model (Product × BranchInventory) from the custom repo — no LINQ here.
        // Active products initialised at the branch; out-of-stock rows are returned so the UI
        // can render them disabled. Only sellable products (raw materials never appear at the POS).
        var rows = await _branchInventoryRepository.GetListWithProductAsync(
            branchId,
            filter,
            onlyOutOfStock: false,
            onlyLowStock: false,
            includeInactiveProducts: false,
            sorting: "ProductName",
            skipCount: 0,
            maxResultCount: 500,
            onlySellable: true);
        var nonExpired = await _stockBatchRepository.GetNonExpiredQuantitiesByProductAsync(
            branchId, Clock.Now.ToUniversalTime().Date);

        return rows.ConvertAll(r => new CashierProductDto
        {
            ProductId = r.Product.Id,
            Name = r.Product.DisplayName,
            SKU = r.Product.SKU,
            Description = r.Product.DisplayDescription,
            SalePrice = r.Product.SalePrice,
            ImageUrl = r.Product.ImageUrl,
            QuantityOnHand = StockBatchManager.GetUsableQuantity(r.Product, r.Inventory, nonExpired),
            IsLowStock = StockBatchManager.GetUsableQuantity(r.Product, r.Inventory, nonExpired) <= r.Inventory.MinimumStock,
            IsOutOfStock = StockBatchManager.GetUsableQuantity(r.Product, r.Inventory, nonExpired) <= 0
        });
    }

    // ─── Sale recording ───

    [Authorize(OperationsPermissions.Cashier.OperatePos)]
    public async Task<CashierSaleResultDto> RecordSaleAsync(RecordCashierSaleDto input)
    {
        if (input.Lines == null || input.Lines.Count == 0)
        {
            throw new BusinessException(OperationsErrorCodes.CannotRecordEmptySale);
        }

        EnsureBranchAllowed(input.BranchId);

        var userId = CurrentUser.GetId();

        // Requires an Open shift for this cashier+branch.
        var shift = await _shiftRepository.FindOpenShiftAsync(input.BranchId, userId)
            ?? throw new BusinessException(OperationsErrorCodes.NoOpenShift)
                .WithData("BranchId", input.BranchId);

        await _branchRepository.GetAsync(input.BranchId);

        // 1) Draft (auto invoice number + uniqueness).
        var sale = await _saleManager.CreateDraftAsync(
            input.BranchId,
            invoiceNumber: null,
            saleDate: Clock.Now,
            currency: await GetDefaultCurrencyAsync(),
            notes: null);

        // 2) Materialise items — unit price is ALWAYS the product's SalePrice (server-set).
        var receiptNames = new Dictionary<Guid, string>();
        var productById = new Dictionary<Guid, AppProduct>();
        foreach (var line in input.Lines)
        {
            var product = await _productRepository.GetAsync(line.ProductId);
            receiptNames[product.Id] = product.DisplayName;
            productById[product.Id] = product;
            sale.AddItem(GuidGenerator.Create(), product.Id, line.Quantity, product.SalePrice);
        }

        // 3) Stock snapshot for every product at this branch.
        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var inventories = await _branchInventoryRepository.GetListAsync(
            x => x.BranchId == sale.BranchId && productIds.Contains(x.ProductId));
        var invByProduct = inventories.ToDictionary(x => x.ProductId);
        var nonExpired = await _stockBatchRepository.GetNonExpiredQuantitiesByProductAsync(
            sale.BranchId, Clock.Now.ToUniversalTime().Date);
        var stockSnapshot = invByProduct.ToDictionary(
            kv => kv.Key,
            kv => StockBatchManager.GetUsableQuantity(productById[kv.Key], kv.Value, nonExpired));

        var blockedProducts = sale.Items
            .Where(i => productById[i.ProductId].ShelfLifeDays.HasValue
                && stockSnapshot.GetValueOrDefault(i.ProductId) < i.Quantity)
            .Select(i => receiptNames.GetValueOrDefault(i.ProductId, "-"))
            .ToList();
        if (blockedProducts.Count > 0)
        {
            throw new BusinessException(OperationsErrorCodes.SaleIncludesExpiredStock)
                .WithData("Products", string.Join(", ", blockedProducts));
        }

        // 4) Domain validates stock and raises SaleRecordedEto.
        sale.Record(stockSnapshot);
        sale.AssignShift(shift.Id);

        sale.EnsureCashTendered(input.CashTendered);

        // 5) Persist so movement rows can reference the sale Id.
        await _saleRepository.InsertAsync(sale, autoSave: true);

        // 6) Decrement stock + movement row per item (MovementType "Sale").
        foreach (var item in sale.Items)
        {
            var inv = invByProduct[item.ProductId];
            var adjustment = await _inventoryManager.AdjustStockDetailedAsync(
                inv,
                inv.QuantityOnHand - item.Quantity,
                StockMovementTypes.Sale,
                notes: sale.InvoiceNumber,
                referenceId: sale.Id,
                referenceType: nameof(AppSale));
            sale.RecordItemSoldBatches(
                item.Id,
                StockTransferBatchBreakdown.Format(
                    adjustment.ConsumedBatches.Select(
                        b => new StockTransferBatchBreakdown.Line(b.ExpiryDate, b.Quantity))));
            await _branchInventoryRepository.UpdateAsync(inv);
        }

        await _saleRepository.UpdateAsync(sale, autoSave: true);

        return new CashierSaleResultDto
        {
            SaleId = sale.Id,
            InvoiceNumber = sale.InvoiceNumber,
            SaleDate = sale.SaleDate,
            Total = sale.TotalAmount,
            ChangeDue = input.CashTendered.HasValue ? input.CashTendered.Value - sale.TotalAmount : null,
            Lines = sale.Items.Select(i => new ReceiptLineDto
            {
                ProductName = receiptNames.GetValueOrDefault(i.ProductId, "-"),
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                Subtotal = i.Subtotal
            }).ToList()
        };
    }

    // ─── Recent sales + void ───

    public async Task<List<RecentSaleDto>> GetRecentSalesAsync(Guid branchId, int withinMinutes = 60)
    {
        EnsureBranchAllowed(branchId);

        var userId = CurrentUser.GetId();
        var window = withinMinutes <= 0 ? VoidWindowMinutes : withinMinutes;
        var since = Clock.Now.AddMinutes(-window);

        var rows = await _saleRepository.GetRecentByCashierAsync(branchId, userId, since);

        return MapRecentSales(rows, window, canVoidAnytime: false);
    }

    public async Task<List<RecentSaleDto>> GetRecentSalesForCurrentUserAsync(int withinMinutes = 60)
    {
        var window = withinMinutes <= 0 ? VoidWindowMinutes : withinMinutes;
        var since = Clock.Now.AddMinutes(-window);
        var canViewAllShifts = await AuthorizationService.IsGrantedAsync(
            OperationsPermissions.Cashier.ViewAllShifts);

        if (!canViewAllShifts)
        {
            var branchId = GetAssignedBranchId();
            if (branchId is null)
            {
                return new List<RecentSaleDto>();
            }

            var cashierRows = await _saleRepository.GetRecentByCashierAsync(
                branchId.Value, CurrentUser.GetId(), since);
            return MapRecentSales(cashierRows, window, canVoidAnytime: false);
        }

        IReadOnlyCollection<Guid>? branchScope = null;
        var canManageAllSales = await AuthorizationService.IsGrantedAsync(
            OperationsPermissions.Sales.ManageAll);
        if (!canManageAllSales)
        {
            branchScope = await GetAccessibleBranchIdsAsync();
        }

        var rows = await _saleRepository.GetFilteredListAsync(
            branchScope,
            branchId: null,
            fromDate: since,
            toDate: null,
            filter: null,
            sorting: "SaleDate desc",
            skipCount: 0,
            maxResultCount: 100);

        return MapRecentSales(rows, window, canManageAllSales);
    }

    public async Task<SaleDto> GetSaleDetailsAsync(Guid saleId)
    {
        var sale = await _saleRepository.GetWithItemsAsync(saleId);

        var canViewAllShifts = await AuthorizationService.IsGrantedAsync(
            OperationsPermissions.Cashier.ViewAllShifts);
        if (canViewAllShifts)
        {
            await EnsureSupervisorBranchAccessAsync(sale.BranchId);
        }
        else
        {
            EnsureBranchAllowed(sale.BranchId);
            if (sale.CreatorId != CurrentUser.GetId())
            {
                throw new BusinessException(OperationsErrorCodes.CashierSaleAccessDenied)
                    .WithData("SaleId", sale.Id);
            }
        }

        return await ProjectSaleAsync(sale);
    }

    public async Task VoidSaleAsync(VoidSaleDto input)
    {
        var sale = await _saleRepository.GetWithItemsAsync(input.SaleId);

        var canManageAnySale = await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll);
        var canViewAllShifts = await AuthorizationService.IsGrantedAsync(
            OperationsPermissions.Cashier.ViewAllShifts);
        if (canViewAllShifts)
        {
            await EnsureSupervisorBranchAccessAsync(sale.BranchId);
        }
        else
        {
            EnsureBranchAllowed(sale.BranchId);
            if (sale.CreatorId != CurrentUser.GetId())
            {
                throw new BusinessException(OperationsErrorCodes.CashierSaleAccessDenied)
                    .WithData("SaleId", sale.Id);
            }
        }

        if (sale.IsVoided)
        {
            throw new BusinessException(OperationsErrorCodes.SaleAlreadyVoided)
                .WithData("SaleId", sale.Id);
        }

        var now = Clock.Now;

        // Managers (Sales.ManageAll) may void anytime; otherwise enforce the 60-min window.
        var canVoidAnytime = canManageAnySale;
        if (!canVoidAnytime && sale.SaleDate < now.AddMinutes(-VoidWindowMinutes))
        {
            throw new BusinessException(OperationsErrorCodes.VoidWindowExpired)
                .WithData("WindowMinutes", VoidWindowMinutes)
                .WithData("SaleId", sale.Id);
        }

        // Restore stock: one SaleReturn movement per line (inv.QuantityOnHand + item.Quantity).
        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var inventories = await _branchInventoryRepository.GetListAsync(
            x => x.BranchId == sale.BranchId && productIds.Contains(x.ProductId));
        var invByProduct = inventories.ToDictionary(x => x.ProductId);
        var products = await _productRepository.GetListAsync(p => productIds.Contains(p.Id));
        var productById = products.ToDictionary(p => p.Id);

        var missingBatchHistory = sale.Items.FirstOrDefault(i =>
            productById.GetValueOrDefault(i.ProductId)?.ShelfLifeDays.HasValue == true
            && string.IsNullOrWhiteSpace(i.SoldBatchBreakdown));
        if (missingBatchHistory != null)
        {
            throw new BusinessException(OperationsErrorCodes.SaleBatchHistoryMissing)
                .WithData("ProductId", missingBatchHistory.ProductId)
                .WithData("ProductName", productById[missingBatchHistory.ProductId].DisplayName);
        }

        foreach (var item in sale.Items)
        {
            if (!invByProduct.TryGetValue(item.ProductId, out var inv))
            {
                continue; // No inventory row to restore into (product removed); skip safely.
            }

            var remaining = item.Quantity;
            foreach (var batch in StockTransferBatchBreakdown.Parse(item.SoldBatchBreakdown))
            {
                if (remaining <= 0) break;
                var quantity = Math.Min(batch.Quantity, remaining);
                remaining -= quantity;
                await _inventoryManager.AdjustStockAsync(
                    inv,
                    inv.QuantityOnHand + quantity,
                    StockMovementTypes.SaleReturn,
                    notes: sale.InvoiceNumber,
                    referenceId: sale.Id,
                    referenceType: nameof(AppSale),
                    batchExpiryDate: batch.ExpiryDate);
            }

            if (remaining > 0)
            {
                await _inventoryManager.AdjustStockAsync(
                    inv,
                    inv.QuantityOnHand + remaining,
                    StockMovementTypes.SaleReturn,
                    notes: sale.InvoiceNumber,
                    referenceId: sale.Id,
                    referenceType: nameof(AppSale));
            }
            await _branchInventoryRepository.UpdateAsync(inv);
        }

        sale.Void(CurrentUser.GetId(), input.Reason, now);
        await _saleRepository.UpdateAsync(sale, autoSave: true);
    }

    // ─── Helpers ───

    /// <summary>
    /// The shop's configured currency. The setting is defined in the host project, so the
    /// key is referenced by its literal name (same pattern as ProductionOrderAppService).
    /// </summary>
    private async Task<string> GetDefaultCurrencyAsync()
        => ShopCurrencySettings.Normalize(
            await _settingProvider.GetOrNullAsync(ShopCurrencySettings.Name));

    /// <summary>
    /// The branch a cashier is assigned to, parsed from their persistent
    /// <see cref="CashierClaimTypes.AssignedBranchId"/> claim. Null when the claim is
    /// absent or unparseable (e.g. an admin operating the POS for testing — they are
    /// allowed to operate any branch).
    /// </summary>
    private Guid? GetAssignedBranchId()
    {
        var raw = CurrentUser.FindClaimValue(CashierClaimTypes.AssignedBranchId);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>
    /// Server-side branch enforcement for the cashier's branch-scoped operations. When
    /// the current user has an AssignedBranchId claim, every branch-scoped call must
    /// target that exact branch; otherwise (no claim — e.g. an admin) any branch is
    /// allowed.
    /// </summary>
    private void EnsureBranchAllowed(Guid branchId)
    {
        var assigned = GetAssignedBranchId();
        if (assigned is not null && assigned.Value != branchId)
        {
            throw new BusinessException(OperationsErrorCodes.BranchNotAssignedToCashier)
                .WithData("BranchId", branchId);
        }
    }

    private async Task EnsureSupervisorBranchAccessAsync(Guid branchId)
    {
        if (await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll))
        {
            return;
        }

        var userId = CurrentUser.Id;
        if (userId is null ||
            !await _branchRepository.AnyAsync(b => b.Id == branchId && b.ManagerUserId == userId))
        {
            throw new BusinessException(OperationsErrorCodes.CashierSaleAccessDenied)
                .WithData("BranchId", branchId);
        }
    }

    private List<RecentSaleDto> MapRecentSales(
        List<SaleListRow> rows,
        int windowMinutes,
        bool canVoidAnytime)
    {
        var cutoff = Clock.Now.AddMinutes(-windowMinutes);
        return rows.ConvertAll(r => new RecentSaleDto
        {
            SaleId = r.Sale.Id,
            InvoiceNumber = r.Sale.InvoiceNumber,
            SaleDate = r.Sale.SaleDate,
            Total = r.Sale.TotalAmount,
            ItemCount = r.ItemCount,
            IsVoided = r.Sale.IsVoided,
            CanVoid = !r.Sale.IsVoided && (canVoidAnytime || r.Sale.SaleDate >= cutoff)
        });
    }

    private async Task<CashierShiftDto> ProjectShiftAsync(AppCashierShift shift, ShiftSalesTotals? totals = null)
    {
        totals ??= await _shiftRepository.GetShiftSalesTotalsAsync(shift.Id);

        var dto = MapShift(shift, totals);

        var branch = await _branchRepository.FindAsync(shift.BranchId);
        dto.BranchName = branch?.DisplayName;

        var user = await _userRepository.FindAsync(shift.CashierUserId);
        dto.CashierUserName = user?.UserName;

        return dto;
    }

    private async Task<SaleDto> ProjectSaleAsync(AppSale sale)
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
            BranchName = branch.DisplayName,
            SaleDate = sale.SaleDate,
            TotalAmount = sale.TotalAmount,
            Currency = await GetDefaultCurrencyAsync(),
            Notes = sale.Notes,
            CreationTime = sale.CreationTime,
            CreatorId = sale.CreatorId,
            CreatorUserName = creatorUserName,
            ItemCount = sale.Items.Count,
            Items = sale.Items.Select(i => ProjectSaleItem(i, productMap.GetValueOrDefault(i.ProductId))).ToList()
        };
    }

    private static SaleItemDto ProjectSaleItem(AppSaleItem item, AppProduct? product) => new()
    {
        Id = item.Id,
        ProductId = item.ProductId,
        ProductName = product?.DisplayName ?? "(deleted product)",
        ProductSKU = product?.SKU ?? "-",
        ProductUnit = product?.DisplayUnit ?? "-",
        Quantity = item.Quantity,
        UnitPrice = item.UnitPrice,
        Subtotal = item.Subtotal
    };

    private static CashierShiftDto MapShift(AppCashierShift shift, ShiftSalesTotals totals) => new()
    {
        Id = shift.Id,
        BranchId = shift.BranchId,
        CashierUserId = shift.CashierUserId,
        OpenedAt = shift.OpenedAt,
        OpeningFloat = shift.OpeningFloat,
        Status = shift.Status,
        SalesCount = totals.SalesCount,
        SalesTotal = totals.NonVoidedTotal,
        VoidedTotal = totals.VoidedTotal,
        // Live expected cash: opening float + non-voided sales (voided already excluded).
        ExpectedCash = shift.ExpectedCash ?? shift.OpeningFloat + totals.NonVoidedTotal,
        CountedCash = shift.CountedCash,
        ClosedAt = shift.ClosedAt,
        Variance = shift.Variance
    };

    private async Task<List<Guid>> GetAccessibleBranchIdsAsync()
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

    private async Task<Dictionary<Guid, string>> GetBranchNamesAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var branches = await _branchRepository.GetListAsync(b => ids.Contains(b.Id));
        return branches.ToDictionary(b => b.Id, b => b.DisplayName);
    }

    private async Task<Dictionary<Guid, string>> GetUserNamesAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var users = await _userRepository.GetListAsync(u => ids.Contains(u.Id));
        return users.ToDictionary(u => u.Id, u => u.UserName);
    }
}
