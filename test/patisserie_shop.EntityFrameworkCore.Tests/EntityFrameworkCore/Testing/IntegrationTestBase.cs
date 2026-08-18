using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Rules;
using Inventory.BranchInventory;
using Inventory.Branches;
using Inventory.Categories;
using Inventory.Products;
using Inventory.Suppliers;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.Domain.Repositories;

namespace patisserie_shop.EntityFrameworkCore.Testing;

/// <summary>
/// Shared arrange-helpers for the integration suite. Every test gets a fresh ABP
/// application + in-memory SQLite database, pre-seeded only by the framework data
/// seeders (admin identity + the five global AppInventoryRules from
/// IntelligenceDataSeedContributor). Tests that assert on decision creation call
/// <see cref="DeactivateAllRulesAsync"/> first so only their own rules can fire.
/// </summary>
public abstract class IntegrationTestBase : patisserie_shopEntityFrameworkCoreTestBase
{
    private int _seq;

    protected string NextName(string prefix) => $"{prefix}-{++_seq:D3}";

    // ── Master data ──

    protected async Task<CategoryDto> CreateCategoryAsync()
    {
        var categories = GetRequiredService<ICategoryAppService>();
        var name = NextName("Category");
        return await categories.CreateAsync(new CreateCategoryDto { NameAr = name, NameEn = name });
    }

    protected async Task<SupplierDto> CreateSupplierAsync(int leadTimeDays = 3)
    {
        var suppliers = GetRequiredService<ISupplierAppService>();
        return await suppliers.CreateAsync(new CreateSupplierDto
        {
            Name = NextName("Supplier"),
            LeadTimeDays = leadTimeDays
        });
    }

    protected async Task<BranchDto> CreateBranchAsync()
    {
        var branches = GetRequiredService<IBranchAppService>();
        var name = NextName("Branch");
        return await branches.CreateAsync(new CreateBranchDto { NameAr = name, NameEn = name });
    }

    protected async Task<ProductDto> CreateProductAsync(
        Guid categoryId,
        Guid? defaultSupplierId = null,
        decimal costPrice = 2m,
        decimal salePrice = 5m,
        int reorderLevel = 5,
        int? shelfLifeDays = null)
    {
        var products = GetRequiredService<IProductAppService>();
        var name = NextName("Product");
        return await products.CreateAsync(new CreateProductDto
        {
            CategoryId = categoryId,
            DefaultSupplierId = defaultSupplierId,
            NameAr = name,
            NameEn = name,
            SKU = $"SKU-{name}",
            UnitAr = "قطعة",
            UnitEn = "pcs",
            CostPrice = costPrice,
            SalePrice = salePrice,
            ReorderLevel = reorderLevel,
            ShelfLifeDays = shelfLifeDays
        });
    }

    // ── Stock ──

    protected async Task<BranchInventoryDto> InitializeInventoryAsync(
        Guid branchId, Guid productId, int quantity, int minimumStock = 0, int? maximumStock = null)
    {
        var inventoryService = GetRequiredService<IBranchInventoryAppService>();
        return await inventoryService.InitializeAsync(new InitializeBranchInventoryDto
        {
            BranchId = branchId,
            ProductId = productId,
            InitialQuantity = quantity,
            MinimumStock = minimumStock,
            MaximumStock = maximumStock
        });
    }

    /// <summary>
    /// Drives the thesis-core path: BranchInventoryAppService.AdjustStockAsync →
    /// BranchInventoryManager → AppBranchInventory.UpdateStock → StockChangedEto →
    /// StockChangedEventHandler → DecisionMakerService.
    /// </summary>
    protected async Task<BranchInventoryDto> AdjustStockAsync(Guid inventoryId, int newQuantity)
    {
        var inventoryService = GetRequiredService<IBranchInventoryAppService>();
        return await inventoryService.AdjustStockAsync(inventoryId, new AdjustStockDto
        {
            NewQuantity = newQuantity
        });
    }

    // ── Rules ──

    /// <summary>Deactivates the globally seeded rules so only test-created rules can fire.</summary>
    protected async Task DeactivateAllRulesAsync()
    {
        var ruleRepository = GetRequiredService<IRepository<AppInventoryRule, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            var rules = await ruleRepository.GetListAsync();
            foreach (var rule in rules)
            {
                rule.Deactivate();
                await ruleRepository.UpdateAsync(rule);
            }
        });
    }

    protected async Task<InventoryRuleDto> CreateRuleAsync(
        string ruleType,
        int? thresholdValue = null,
        int? thresholdDays = null,
        Guid? productId = null,
        Guid? branchId = null,
        int priority = 0,
        string? suggestedAction = null)
    {
        var ruleService = GetRequiredService<IInventoryRuleAppService>();
        return await ruleService.CreateAsync(new CreateInventoryRuleDto
        {
            RuleName = NextName($"Rule-{ruleType}"),
            RuleType = ruleType,
            ProductId = productId,
            BranchId = branchId,
            ThresholdValue = thresholdValue,
            ThresholdDays = thresholdDays,
            Priority = priority,
            SuggestedAction = suggestedAction,
            IsActive = true
        });
    }

    // ── Decision logs ──

    protected async Task<List<AppDecisionLog>> GetDecisionLogsAsync(Guid productId)
    {
        var logRepository = GetRequiredService<IRepository<AppDecisionLog, Guid>>();
        return await WithUnitOfWorkAsync(() =>
            logRepository.GetListAsync(l => l.ProductId == productId));
    }

    /// <summary>
    /// Inserts a decision ledger row whose CreationTime lies in the past. ABP's audit
    /// property setter only stamps CreationTime when it is still default, so pre-setting
    /// it (protected setter → reflection, test-side only) back-dates the row — this is
    /// how the 48h outcome-evaluation delay is simulated without faking IClock.
    /// </summary>
    protected async Task<AppDecisionLog> InsertDecisionLogAsync(AppDecisionLog log, DateTime? backdatedCreationTimeUtc = null)
    {
        if (backdatedCreationTimeUtc.HasValue)
        {
            SetCreationTime(log, backdatedCreationTimeUtc.Value);
        }

        var logRepository = GetRequiredService<IRepository<AppDecisionLog, Guid>>();
        return await WithUnitOfWorkAsync(() => logRepository.InsertAsync(log, autoSave: true));
    }

    private static void SetCreationTime(AppDecisionLog log, DateTime creationTimeUtc)
    {
        var property = typeof(CreationAuditedAggregateRoot<Guid>)
            .GetProperty(nameof(AppDecisionLog.CreationTime), BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException("CreationTime property not found.");
        property.SetValue(log, creationTimeUtc);
    }
}
