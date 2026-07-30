using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.Entities;
using Microsoft.Extensions.Logging;
using Operations.Entities;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace patisserie_shop.DbMigrator;

/// <summary>
/// Creates ninety days of deterministic sales for every sales branch. The rows are inserted
/// as historical ledger data, without publishing SaleRecordedEto, so the carefully staged
/// current stock balances remain unchanged. Every sale is attributed to that branch's cashier.
/// </summary>
public class SalesHistorySeedContributor : IDataSeedContributor, ITransientDependency
{
    private const int HistoryDays = 90;

    private readonly IRepository<AppSale, Guid> _saleRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<SalesHistorySeedContributor> _logger;

    public SalesHistorySeedContributor(
        IRepository<AppSale, Guid> saleRepository,
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IdentityUserManager userManager,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IGuidGenerator guidGenerator,
        ILogger<SalesHistorySeedContributor> logger)
    {
        _saleRepository = saleRepository;
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _userManager = userManager;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _saleRepository.AnyAsync(s => s.SaleDate < DateTime.UtcNow.AddDays(-7)))
        {
            _logger.LogInformation("[Seed] Historical sales already exist; sales history was preserved.");
            return;
        }

        var products = (await _productRepository.GetListAsync())
            .Where(p => p.IsActive
                        && p.IsSellable
                        && p.ProductType == ProductTypes.FinishedGood)
            .Select(p => (Product: p, Weight: GetPopularityWeight(p.SKU)))
            .ToList();
        if (products.Count == 0)
        {
            _logger.LogWarning("[Seed] Sales history could not be created because no sellable finished products exist.");
            return;
        }

        var branches = await _branchRepository.GetListAsync(
            b => b.IsActive && b.BranchType == BranchTypes.SalesBranch);
        var branchesByName = branches.ToDictionary(b => b.Name);
        var endDay = DateTime.UtcNow.Date.AddDays(-1);
        var startDay = endDay.AddDays(-(HistoryDays - 1));
        var totalSales = 0;
        var totalItems = 0;

        for (var branchIndex = 0; branchIndex < GraduationSeedData.RetailBranches.Count; branchIndex++)
        {
            var spec = GraduationSeedData.RetailBranches[branchIndex];
            if (!branchesByName.TryGetValue(spec.Name, out var branch) || spec.CashierUserName == null)
            {
                continue;
            }

            var cashier = await _userManager.FindByNameAsync(spec.CashierUserName)
                ?? throw new InvalidOperationException(
                    $"Presentation cashier '{spec.CashierUserName}' was not found.");
            using var actorScope = _currentPrincipalAccessor.Change(
                GraduationSeedData.PrincipalFor(
                    cashier,
                    IdentityDataSeedContributor.CashierRoleName));

            var rng = new Random(20260730 + branchIndex * 97);
            var recentPool = GetRecentProductPool(spec.Name, products);
            var branchBatch = new List<AppSale>();

            for (var dayIndex = 0; dayIndex < HistoryDays; dayIndex++)
            {
                var day = startDay.AddDays(dayIndex);
                var trendFactor = 0.92 + 0.16 * dayIndex / (HistoryDays - 1);
                var busyDayFactor = day.DayOfWeek switch
                {
                    DayOfWeek.Thursday => 1.18,
                    DayOfWeek.Friday => 1.35,
                    DayOfWeek.Saturday => 1.15,
                    _ => 1.0
                };
                var baseCount = rng.Next(spec.MinSalesPerDay, spec.MaxSalesPerDay + 1);
                var saleCount = Math.Max(
                    1,
                    (int)Math.Round(baseCount * trendFactor * busyDayFactor));

                for (var sequence = 1; sequence <= saleCount; sequence++)
                {
                    var pool = day >= endDay.AddDays(-40) ? recentPool : products;
                    var sale = BuildSale(
                        rng,
                        branch.Id,
                        branchIndex,
                        day,
                        sequence,
                        pool);
                    branchBatch.Add(sale);
                    totalSales++;
                    totalItems += sale.Items.Count;
                }

                if ((dayIndex + 1) % 7 == 0 || dayIndex == HistoryDays - 1)
                {
                    await _saleRepository.InsertManyAsync(branchBatch, autoSave: true);
                    branchBatch.Clear();
                }
            }
        }

        _logger.LogInformation(
            "[Seed] Created {Sales} historical sales with {Items} lines across {Branches} branches and {Days} days.",
            totalSales,
            totalItems,
            GraduationSeedData.RetailBranches.Count,
            HistoryDays);
    }

    private AppSale BuildSale(
        Random rng,
        Guid branchId,
        int branchIndex,
        DateTime day,
        int sequence,
        List<(AppProduct Product, int Weight)> weighted)
    {
        var invoiceNumber = $"INV-{day:yyyyMMdd}-{branchIndex + 1:D2}{sequence:D3}";
        var saleDate = day.AddHours(7).AddMinutes(rng.Next(0, 13 * 60 + 1));
        var notes = rng.Next(0, 10) switch
        {
            0 => "طلب استلام من الفرع.",
            1 => "طلب ضيافة لمكتب قريب.",
            2 => "تم تطبيق عرض الصباح.",
            _ => null
        };

        var sale = new AppSale(
            _guidGenerator.Create(),
            branchId,
            invoiceNumber,
            saleDate,
            currency: "USD",
            notes);

        var lineCount = rng.Next(1, 6);
        var pool = new List<(AppProduct Product, int Weight)>(weighted);
        for (var line = 0; line < lineCount && pool.Count > 0; line++)
        {
            var product = PickWeighted(rng, pool);
            var maxQuantity = product.SalePrice >= 20m ? 2 : 6;
            var quantity = rng.Next(1, maxQuantity + 1);
            var unitPrice = rng.NextDouble() < 0.12
                ? Math.Round(product.SalePrice * 0.90m, 2, MidpointRounding.AwayFromZero)
                : product.SalePrice;
            sale.AddItem(_guidGenerator.Create(), product.Id, quantity, unitPrice);
        }

        return sale;
    }

    private static List<(AppProduct Product, int Weight)> GetRecentProductPool(
        string branchName,
        List<(AppProduct Product, int Weight)> products)
    {
        var excludedSku = branchName switch
        {
            "فرع المزة" => "SS-001",
            "فرع المالكي" => "SS-002",
            "فرع مشروع دمر" => "PF-005",
            "فرع جرمانا" => "SS-003",
            _ => null
        };

        return excludedSku == null
            ? products
            : products.Where(x => x.Product.SKU != excludedSku).ToList();
    }

    private static AppProduct PickWeighted(
        Random rng,
        List<(AppProduct Product, int Weight)> pool)
    {
        var roll = rng.Next(0, pool.Sum(x => x.Weight));
        for (var index = 0; index < pool.Count; index++)
        {
            roll -= pool[index].Weight;
            if (roll < 0)
            {
                var picked = pool[index].Product;
                pool.RemoveAt(index);
                return picked;
            }
        }

        var fallback = pool[^1].Product;
        pool.RemoveAt(pool.Count - 1);
        return fallback;
    }

    private static int GetPopularityWeight(string sku)
    {
        if (sku.StartsWith("VN-", StringComparison.OrdinalIgnoreCase)) return 10;
        if (sku.StartsWith("BR-", StringComparison.OrdinalIgnoreCase)) return 9;
        if (sku.StartsWith("PF-", StringComparison.OrdinalIgnoreCase)) return 5;
        if (sku.StartsWith("CB-", StringComparison.OrdinalIgnoreCase)) return 5;
        if (sku.StartsWith("CT-", StringComparison.OrdinalIgnoreCase)) return 3;
        if (sku.StartsWith("SS-", StringComparison.OrdinalIgnoreCase)) return 2;
        return 4;
    }
}
