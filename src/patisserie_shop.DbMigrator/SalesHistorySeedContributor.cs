using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Microsoft.Extensions.Logging;
using Operations.Entities;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Uow;

namespace patisserie_shop.DbMigrator;

/// <summary>
/// Seeds ~90 days of historical demo sales for the two retail branches
/// (Main Street Boutique and Riverside Café) so the dashboards and charts
/// have realistic history on a fresh database.
///
/// Runs AFTER <see cref="PatisserieDataSeedContributor"/> (ordering enforced in
/// <see cref="patisserie_shopDbMigratorModule"/> via AbpDataSeedOptions) and bails
/// out gracefully if the expected branches/products are missing.
///
/// IMPORTANT: historical sales are inserted silently — AppSale.Record() is
/// deliberately NOT called, so no SaleRecordedEto is published and current
/// stock levels are not touched. These rows are pure ledger history.
///
/// Idempotent: skips entirely if any HIST- invoice already exists, or if the
/// database already contains sales older than 7 days (i.e. history of any kind).
/// </summary>
public class SalesHistorySeedContributor : IDataSeedContributor, ITransientDependency
{
    public const string HistoricalInvoicePrefix = "HIST-";

    private const int HistoryDays = 90;
    private const string MainStreetBranchName = "بوتيك الشارع الرئيسي";
    private const string RiversideBranchName = "مقهى ضفة النهر";

    private readonly IRepository<AppSale, Guid> _saleRepo;
    private readonly IRepository<AppProduct, Guid> _productRepo;
    private readonly IRepository<AppBranch, Guid> _branchRepo;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<SalesHistorySeedContributor> _logger;

    public SalesHistorySeedContributor(
        IRepository<AppSale, Guid> saleRepo,
        IRepository<AppProduct, Guid> productRepo,
        IRepository<AppBranch, Guid> branchRepo,
        IGuidGenerator guidGenerator,
        ILogger<SalesHistorySeedContributor> logger)
    {
        _saleRepo = saleRepo;
        _productRepo = productRepo;
        _branchRepo = branchRepo;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        // ── Idempotency guard ──
        var historyCutoff = DateTime.UtcNow.AddDays(-7);
        var alreadySeeded = await _saleRepo.AnyAsync(s =>
            s.InvoiceNumber.StartsWith(HistoricalInvoicePrefix) || s.SaleDate < historyCutoff);
        if (alreadySeeded)
        {
            _logger.LogInformation("[Seed] Historical sales already exist — skipping sales history seeding.");
            return;
        }

        // ── Prerequisites (seeded by PatisserieDataSeedContributor) ──
        var branches = await _branchRepo.GetListAsync();
        var mainStreet = branches.FirstOrDefault(b => b.Name == MainStreetBranchName);
        var riverside = branches.FirstOrDefault(b => b.Name == RiversideBranchName);
        if (mainStreet == null || riverside == null)
        {
            _logger.LogWarning(
                "[Seed] Sales history seeding skipped — expected retail branches not found. " +
                "Existing branches: {Names}. Re-run the migrator after the demo branches exist.",
                string.Join(", ", branches.Select(b => b.Name)));
            return;
        }

        var products = (await _productRepo.GetListAsync()).Where(p => p.IsActive).ToList();
        if (products.Count == 0)
        {
            _logger.LogWarning("[Seed] Sales history seeding skipped — no active products found.");
            return;
        }

        // Popularity weights by SKU prefix: viennoiseries/breads sell most,
        // petit fours / cookies mid, cakes less, seasonal least.
        var weighted = products
            .Select(p => (Product: p, Weight: GetPopularityWeight(p.SKU)))
            .ToList();

        // Fixed seed → deterministic, reproducible demo data.
        var rng = new Random(20260610);

        // 90 days ending yesterday (UTC dates).
        var endDay = DateTime.UtcNow.Date.AddDays(-1);
        var startDay = endDay.AddDays(-(HistoryDays - 1));

        // (branch, weekday base min/max) — Main Street runs slightly busier.
        var retailBranches = new[]
        {
            (Branch: mainStreet, MinPerDay: 5, MaxPerDay: 10),
            (Branch: riverside, MinPerDay: 4, MaxPerDay: 8),
        };

        // Keep the dead-stock scenarios staged by PatisserieDataSeedContributor
        // coherent: Main Street's seasonal items were "last sold" 35–45 days ago,
        // Riverside's Profiterole Tower (PF-005) 40 days ago — so no historical
        // sales of those products inside their dead windows.
        var mainSeasonalCutoff = endDay.AddDays(-45);
        var riversidePf005Cutoff = endDay.AddDays(-40);
        var mainRecentPool = weighted
            .Where(x => !x.Product.SKU.StartsWith("SS-", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var riversideRecentPool = weighted
            .Where(x => !x.Product.SKU.Equals("PF-005", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var batch = new List<AppSale>();
        var totalSales = 0;
        var totalItems = 0;

        for (var dayIndex = 0; dayIndex < HistoryDays; dayIndex++)
        {
            var day = startDay.AddDays(dayIndex);

            // Gentle upward trend: +~15% volume over the 90-day window.
            var trendFactor = 1.0 + 0.15 * dayIndex / (HistoryDays - 1);
            var weekendFactor = day.DayOfWeek switch
            {
                DayOfWeek.Friday => 1.3,
                DayOfWeek.Saturday => 1.6,
                DayOfWeek.Sunday => 1.4,
                _ => 1.0,
            };

            var daySeq = 0;
            foreach (var (branch, minPerDay, maxPerDay) in retailBranches)
            {
                var pool = weighted;
                if (branch == mainStreet && day >= mainSeasonalCutoff)
                {
                    pool = mainRecentPool;
                }
                else if (branch == riverside && day >= riversidePf005Cutoff)
                {
                    pool = riversideRecentPool;
                }

                var baseCount = rng.Next(minPerDay, maxPerDay + 1);
                var saleCount = Math.Max(1, (int)Math.Round(baseCount * weekendFactor * trendFactor));

                for (var i = 0; i < saleCount; i++)
                {
                    daySeq++;
                    var sale = BuildSale(rng, branch.Id, day, daySeq, pool);
                    totalSales++;
                    totalItems += sale.Items.Count;
                    batch.Add(sale);
                }
            }

            // Flush once per week to keep the change tracker small.
            if ((dayIndex + 1) % 7 == 0 || dayIndex == HistoryDays - 1)
            {
                await _saleRepo.InsertManyAsync(batch, autoSave: true);
                batch.Clear();
            }
        }

        _logger.LogInformation(
            "[Seed] Seeded {Sales} historical sales ({Items} line items) across {Days} days " +
            "for {MainStreet} and {Riverside}.",
            totalSales, totalItems, HistoryDays, MainStreetBranchName, RiversideBranchName);
    }

    private AppSale BuildSale(
        Random rng,
        Guid branchId,
        DateTime day,
        int daySeq,
        List<(AppProduct Product, int Weight)> weighted)
    {
        // Invoice format clearly distinct from the app's INV-YYYY-NNNN sequence,
        // unique per (day, sequence-within-day) — no collision risk.
        var invoiceNumber = $"{HistoricalInvoicePrefix}{day:yyyyMMdd}-{daySeq:D3}";

        // Sale time spread across business hours, 07:00–19:00.
        var saleDate = day.AddHours(7).AddMinutes(rng.Next(0, 12 * 60 + 1));

        var sale = new AppSale(
            _guidGenerator.Create(),
            branchId,
            invoiceNumber,
            saleDate,
            currency: "USD",
            notes: "بيع تجريبي تاريخي مُدخَل");

        // 1–5 distinct products per sale, popularity-weighted without replacement.
        var lineCount = rng.Next(1, 6);
        var pool = new List<(AppProduct Product, int Weight)>(weighted);

        for (var line = 0; line < lineCount && pool.Count > 0; line++)
        {
            var product = PickWeighted(rng, pool);

            // Big-ticket items (whole cakes, etc.) sell 1–2 at a time; the rest 1–6.
            var maxQty = product.SalePrice >= 20m ? 2 : 6;
            var quantity = rng.Next(1, maxQty + 1);

            // Occasional small price variation (promo / day-old discount, ±5%).
            var unitPrice = product.SalePrice;
            if (rng.NextDouble() < 0.15)
            {
                var variation = (decimal)(0.95 + rng.NextDouble() * 0.10);
                unitPrice = Math.Round(unitPrice * variation, 2, MidpointRounding.AwayFromZero);
            }

            sale.AddItem(_guidGenerator.Create(), product.Id, quantity, unitPrice);
        }

        // NOTE: sale.Record(...) is intentionally NOT called — see class docs.
        return sale;
    }

    private static AppProduct PickWeighted(Random rng, List<(AppProduct Product, int Weight)> pool)
    {
        var totalWeight = pool.Sum(x => x.Weight);
        var roll = rng.Next(0, totalWeight);

        for (var i = 0; i < pool.Count; i++)
        {
            roll -= pool[i].Weight;
            if (roll < 0)
            {
                var picked = pool[i].Product;
                pool.RemoveAt(i); // without replacement — AddItem forbids duplicate products
                return picked;
            }
        }

        // Unreachable when totalWeight > 0; defensive fallback.
        var last = pool[^1].Product;
        pool.RemoveAt(pool.Count - 1);
        return last;
    }

    /// <summary>
    /// Demo SKUs are prefixed by category (see PatisserieDataSeedContributor):
    /// VN viennoiseries, BR breads, PF petit fours, CB cookies, CT cakes & tarts,
    /// SS seasonal specials. Unknown SKUs get a middle-of-the-road weight.
    /// </summary>
    private static int GetPopularityWeight(string sku)
    {
        if (sku.StartsWith("VN-", StringComparison.OrdinalIgnoreCase)) return 10;
        if (sku.StartsWith("BR-", StringComparison.OrdinalIgnoreCase)) return 9;
        if (sku.StartsWith("PF-", StringComparison.OrdinalIgnoreCase)) return 5;
        if (sku.StartsWith("CB-", StringComparison.OrdinalIgnoreCase)) return 5;
        if (sku.StartsWith("CT-", StringComparison.OrdinalIgnoreCase)) return 3;
        if (sku.StartsWith("SS-", StringComparison.OrdinalIgnoreCase)) return 1;
        return 4;
    }
}
