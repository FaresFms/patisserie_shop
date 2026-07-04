using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Microsoft.AspNetCore.Authorization;
using Operations.Cashiers;
using Operations.Entities;
using Operations.Permissions;
using Operations.Sales;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Operations.Cashier;

/// <summary>
/// Cash-only POS backend. Thin orchestrator: it composes the existing SaleManager /
/// AppSale.Record / BranchInventoryManager.AdjustStockAsync pipeline (exactly like
/// SaleAppService) plus the cash-drawer aggregate. A cashier is gated by
/// <see cref="OperationsPermissions.Cashier.Default"/> — NOT by being a branch manager,
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

    public CashierAppService(
        ISaleRepository saleRepository,
        ICashierShiftRepository shiftRepository,
        CashierShiftManager shiftManager,
        SaleManager saleManager,
        BranchInventoryManager inventoryManager,
        IBranchInventoryRepository branchInventoryRepository,
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<IdentityUser, Guid> userRepository)
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
    }

    // ─── Shifts ───

    public async Task<CashierShiftDto?> GetCurrentShiftAsync(Guid branchId)
    {
        EnsureBranchAllowed(branchId);

        var userId = CurrentUser.GetId();
        var shift = await _shiftRepository.FindOpenShiftAsync(branchId, userId);
        return shift == null ? null : await ProjectShiftAsync(shift);
    }

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

        return new CashierBranchDto { Id = branch.Id, Name = branch.Name };
    }

    // ─── Branch list (cashier-permitted; Id + Name only) ───

    public async Task<List<CashierBranchDto>> GetSellableBranchesAsync()
    {
        // Active branches only. Gated by the class-level Cashier.Default permission, so a
        // cashier can populate the POS branch picker without Inventory's Branches.Default.
        var branches = await _branchRepository.GetListAsync(b => b.IsActive);

        return branches
            .OrderBy(b => b.Name)
            .Select(b => new CashierBranchDto { Id = b.Id, Name = b.Name })
            .ToList();
    }

    // ─── Product tiles (price + flags only; never cost or quantity) ───

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

        return rows.ConvertAll(r => new CashierProductDto
        {
            ProductId = r.Product.Id,
            Name = r.Product.Name,
            SKU = r.Product.SKU,
            Description = r.Product.Description,
            SalePrice = r.Product.SalePrice,
            ImageUrl = r.Product.ImageUrl,
            QuantityOnHand = r.Inventory.QuantityOnHand,
            IsLowStock = r.Inventory.IsLowStock,
            IsOutOfStock = r.Inventory.IsOutOfStock
        });
    }

    // ─── Sale recording ───

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
            currency: "USD",
            notes: null);

        // 2) Materialise items — unit price is ALWAYS the product's SalePrice (server-set).
        var receiptNames = new Dictionary<Guid, string>();
        foreach (var line in input.Lines)
        {
            var product = await _productRepository.GetAsync(line.ProductId);
            receiptNames[product.Id] = product.Name;
            sale.AddItem(GuidGenerator.Create(), product.Id, line.Quantity, product.SalePrice);
        }

        // 3) Stock snapshot for every product at this branch.
        var productIds = sale.Items.Select(i => i.ProductId).Distinct().ToList();
        var inventories = await _branchInventoryRepository.GetListAsync(
            x => x.BranchId == sale.BranchId && productIds.Contains(x.ProductId));
        var invByProduct = inventories.ToDictionary(x => x.ProductId);
        var stockSnapshot = invByProduct.ToDictionary(kv => kv.Key, kv => kv.Value.QuantityOnHand);

        // 4) Domain validates stock and raises SaleRecordedEto.
        sale.Record(stockSnapshot);
        sale.AssignShift(shift.Id);

        // 5) Persist so movement rows can reference the sale Id.
        await _saleRepository.InsertAsync(sale, autoSave: true);

        // 6) Decrement stock + movement row per item (MovementType "Sale").
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

        var now = Clock.Now;
        return rows.ConvertAll(r => new RecentSaleDto
        {
            SaleId = r.Sale.Id,
            InvoiceNumber = r.Sale.InvoiceNumber,
            SaleDate = r.Sale.SaleDate,
            Total = r.Sale.TotalAmount,
            ItemCount = r.ItemCount,
            IsVoided = r.Sale.IsVoided,
            CanVoid = !r.Sale.IsVoided && r.Sale.SaleDate >= now.AddMinutes(-window)
        });
    }

    public async Task<SaleDto> GetSaleDetailsAsync(Guid saleId)
    {
        var sale = await _saleRepository.GetWithItemsAsync(saleId);
        EnsureBranchAllowed(sale.BranchId);

        var canViewAnyCashierSale = await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll);
        if (!canViewAnyCashierSale && sale.CreatorId != CurrentUser.GetId())
        {
            throw new BusinessException(OperationsErrorCodes.CashierSaleAccessDenied)
                .WithData("SaleId", sale.Id);
        }

        return await ProjectSaleAsync(sale);
    }

    public async Task VoidSaleAsync(VoidSaleDto input)
    {
        var sale = await _saleRepository.GetWithItemsAsync(input.SaleId);

        // Branch isolation: a cashier may only void sales at their assigned branch.
        // Managers with Sales.ManageAll (no branch claim) are unaffected.
        EnsureBranchAllowed(sale.BranchId);

        // Ownership: a non-manager may only void their own sale. Managers with
        // Sales.ManageAll may void any sale at an allowed branch. Mirrors the
        // access rule GetSaleDetailsAsync already enforces.
        var canManageAnySale = await AuthorizationService.IsGrantedAsync(OperationsPermissions.Sales.ManageAll);
        if (!canManageAnySale && sale.CreatorId != CurrentUser.GetId())
        {
            throw new BusinessException(OperationsErrorCodes.CashierSaleAccessDenied)
                .WithData("SaleId", sale.Id);
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

        foreach (var item in sale.Items)
        {
            if (!invByProduct.TryGetValue(item.ProductId, out var inv))
            {
                continue; // No inventory row to restore into (product removed); skip safely.
            }

            await _inventoryManager.AdjustStockAsync(
                inv,
                inv.QuantityOnHand + item.Quantity,
                StockMovementTypes.SaleReturn,
                notes: sale.InvoiceNumber,
                referenceId: sale.Id,
                referenceType: nameof(AppSale));
            await _branchInventoryRepository.UpdateAsync(inv);
        }

        sale.Void(CurrentUser.GetId(), input.Reason, now);
        await _saleRepository.UpdateAsync(sale, autoSave: true);
    }

    // ─── Helpers ───

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

    private async Task<CashierShiftDto> ProjectShiftAsync(AppCashierShift shift, ShiftSalesTotals? totals = null)
    {
        totals ??= await _shiftRepository.GetShiftSalesTotalsAsync(shift.Id);

        var dto = MapShift(shift, totals);

        var branch = await _branchRepository.FindAsync(shift.BranchId);
        dto.BranchName = branch?.Name;

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
            BranchName = branch.Name,
            SaleDate = sale.SaleDate,
            TotalAmount = sale.TotalAmount,
            Currency = sale.Currency,
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
        ProductName = product?.Name ?? "(deleted product)",
        ProductSKU = product?.SKU ?? "-",
        ProductUnit = product?.Unit ?? "-",
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
        return branches.ToDictionary(b => b.Id, b => b.Name);
    }

    private async Task<Dictionary<Guid, string>> GetUserNamesAsync(List<Guid> ids)
    {
        if (ids.Count == 0) return new Dictionary<Guid, string>();
        var users = await _userRepository.GetListAsync(u => ids.Contains(u.Id));
        return users.ToDictionary(u => u.Id, u => u.UserName);
    }
}
