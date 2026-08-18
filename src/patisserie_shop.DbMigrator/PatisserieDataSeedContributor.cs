using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence.Rules;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Branches;
using Inventory.Categories;
using Inventory.Entities;
using Inventory.Products;
using Inventory.StockBatches;
using Inventory.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Operations;
using patisserie_shop.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace patisserie_shop.DbMigrator;

public class PatisserieDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly CategoryManager _categoryManager;
    private readonly SupplierManager _supplierManager;
    private readonly ProductManager _productManager;
    private readonly BranchManager _branchManager;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly StockBatchManager _stockBatchManager;
    private readonly InventoryRuleManager _ruleManager;

    private readonly IRepository<AppCategory, Guid> _categoryRepo;
    private readonly IRepository<AppSupplier, Guid> _supplierRepo;
    private readonly IRepository<AppProduct, Guid> _productRepo;
    private readonly IRepository<AppBranch, Guid> _branchRepo;
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepo;
    private readonly IRepository<AppStockBatch, Guid> _batchRepo;
    private readonly IRepository<AppInventoryRule, Guid> _ruleRepo;

    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IDbContextProvider<patisserie_shopDbContext> _dbContextProvider;
    private readonly ILogger<PatisserieDataSeedContributor> _logger;

    public PatisserieDataSeedContributor(
        CategoryManager categoryManager,
        SupplierManager supplierManager,
        ProductManager productManager,
        BranchManager branchManager,
        BranchInventoryManager inventoryManager,
        StockBatchManager stockBatchManager,
        InventoryRuleManager ruleManager,
        IRepository<AppCategory, Guid> categoryRepo,
        IRepository<AppSupplier, Guid> supplierRepo,
        IRepository<AppProduct, Guid> productRepo,
        IRepository<AppBranch, Guid> branchRepo,
        IRepository<AppBranchInventory, Guid> inventoryRepo,
        IRepository<AppStockBatch, Guid> batchRepo,
        IRepository<AppInventoryRule, Guid> ruleRepo,
        IdentityUserManager userManager,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IDbContextProvider<patisserie_shopDbContext> dbContextProvider,
        ILogger<PatisserieDataSeedContributor> logger)
    {
        _categoryManager = categoryManager;
        _supplierManager = supplierManager;
        _productManager = productManager;
        _branchManager = branchManager;
        _inventoryManager = inventoryManager;
        _stockBatchManager = stockBatchManager;
        _ruleManager = ruleManager;
        _categoryRepo = categoryRepo;
        _supplierRepo = supplierRepo;
        _productRepo = productRepo;
        _branchRepo = branchRepo;
        _inventoryRepo = inventoryRepo;
        _batchRepo = batchRepo;
        _ruleRepo = ruleRepo;
        _userManager = userManager;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _dbContextProvider = dbContextProvider;
        _logger = logger;
    }

    /// <summary>
    /// Shelf life per seeded SKU (days). Viennoiseries/breads spoil in 2–3 days,
    /// cakes/tarts in 3–5, petit fours/cookies last 7–14; the chocolate Easter egg
    /// is shelf-stable for months. Products absent from this map are non-perishable.
    /// </summary>
    private static readonly Dictionary<string, int> ShelfLifeBySku = new()
    {
        ["VN-001"] = 2, ["VN-002"] = 2, ["VN-003"] = 2, ["VN-004"] = 3,
        ["CT-001"] = 3, ["CT-002"] = 3, ["CT-003"] = 2, ["CT-004"] = 3, ["CT-005"] = 3,
        ["BR-001"] = 1, ["BR-002"] = 2, ["BR-003"] = 3, ["BR-004"] = 2,
        ["PF-001"] = 14, ["PF-002"] = 14, ["PF-003"] = 14, ["PF-004"] = 7, ["PF-005"] = 10,
        ["CB-001"] = 14, ["CB-002"] = 10, ["CB-003"] = 14,
        ["SS-001"] = 21, ["SS-002"] = 2, ["SS-003"] = 2,
    };

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        var admin = await _userManager.FindByNameAsync("admin");
        using var actorScope = admin == null
            ? null
            : _currentPrincipalAccessor.Change(
                GraduationSeedData.PrincipalFor(admin, IdentityDataSeedContributor.AdminRoleName));

        var categories = await SeedCategoriesAsync();
        var suppliers  = await SeedSuppliersAsync();
        var products   = await SeedProductsAsync(categories, suppliers);
        await BackfillShelfLifeAsync(products);
        var branches   = await SeedBranchesAsync();
        await SeedInventoryAsync(products, branches);
        await SeedStockBatchesAsync(products, branches);
        await SeedDemoRulesAsync();
    }

    // ─────────────────────────── Categories ───────────────────────────

    private async Task<Dictionary<string, AppCategory>> SeedCategoriesAsync()
    {
        if (await _categoryRepo.CountAsync() > 0)
        {
            _logger.LogInformation("[Seed] Categories already exist — skipping.");
            var existing = await _categoryRepo.GetListAsync();
            return existing.ToDictionary(c => c.NameAr);
        }

        var defs = new[]
        {
            ("المعجنات المورّقة", "Viennoiseries", "معجنات مورّقة مثل الكرواسون وبان أو شوكولا", "Laminated pastries such as croissants and pain au chocolat"),
            ("الكيك والتارت", "Cakes and Tarts", "كيك بالحجم الكامل وأفراد، وفطائر الفاكهة (تارت)", "Whole cakes, individual slices, and fruit tarts"),
            ("الخبز", "Bread", "خبز حرفي يومي وأرغفة الباغيت", "Daily artisan bread and baguettes"),
            ("الحلويات الصغيرة", "Petit Fours", "حلويات بحجم اللقمة، ماكارون، وإكلير صغير", "Bite-sized pastries, macarons, and mini eclairs"),
            ("الكوكيز والبسكويت", "Cookies and Biscuits", "كوكيز بالزبدة، سابليه، وبسكوتي", "Butter cookies, sable biscuits, and biscotti"),
            ("الأصناف الموسمية", "Seasonal Items", "أصناف موسمية واحتفالية متجدّدة", "Rotating seasonal and celebration items"),
        };

        var result = new Dictionary<string, AppCategory>();
        foreach (var (nameAr, nameEn, descriptionAr, descriptionEn) in defs)
        {
            var cat = await _categoryManager.CreateAsync(nameAr, nameEn, descriptionAr, descriptionEn);
            await _categoryRepo.InsertAsync(cat, autoSave: true);
            result[nameAr] = cat;
        }

        _logger.LogInformation("[Seed] Seeded {Count} categories.", result.Count);
        return result;
    }

    // ─────────────────────────── Suppliers ────────────────────────────

    private async Task<Dictionary<string, AppSupplier>> SeedSuppliersAsync()
    {
        if (await _supplierRepo.CountAsync() > 0)
        {
            _logger.LogInformation("[Seed] Suppliers already exist — skipping.");
            var existing = await _supplierRepo.GetListAsync();
            return existing.ToDictionary(s => s.Name);
        }

        var defs = new[]
        {
            ("مطاحن بردى الحديثة",       "أحمد الخباز",  "+963-11-456-7890", "orders@barada-mills.example", "طريق المطار، دمشق"),
            ("ألبان الغوطة",             "مريم الحلبي",  "+963-11-567-8901", "supply@ghouta-dairy.example", "صحنايا، ريف دمشق"),
            ("شركة كاكاو الشام",          "لؤي رستم",     "+963-11-678-9012", "orders@sham-cocoa.example",   "المنطقة الصناعية، عدرا"),
            ("دار العبوة للتغليف",        "سارة مارديني", "+963-11-789-0123", "info@daralobwa.example",      "شارع الصناعة، دمشق"),
        };

        var result = new Dictionary<string, AppSupplier>();
        foreach (var (name, contact, phone, email, address) in defs)
        {
            var s = await _supplierManager.CreateAsync(name, contact, phone, email, address);
            await _supplierRepo.InsertAsync(s, autoSave: true);
            result[name] = s;
        }

        _logger.LogInformation("[Seed] Seeded {Count} suppliers.", result.Count);
        return result;
    }

    // ─────────────────────────── Products ─────────────────────────────

    private async Task<Dictionary<string, AppProduct>> SeedProductsAsync(
        Dictionary<string, AppCategory> cats,
        Dictionary<string, AppSupplier> sups)
    {
        if (await _productRepo.CountAsync() > 0)
        {
            _logger.LogInformation("[Seed] Products already exist — skipping.");
            var existing = await _productRepo.GetListAsync();
            return existing.ToDictionary(p => p.SKU);
        }

        var viennoiseries = cats["المعجنات المورّقة"].Id;
        var cakesTarts    = cats["الكيك والتارت"].Id;
        var breads        = cats["الخبز"].Id;
        var petitFours    = cats["الحلويات الصغيرة"].Id;
        var cookies       = cats["الكوكيز والبسكويت"].Id;
        var seasonal      = cats["الأصناف الموسمية"].Id;

        var moulins  = sups["مطاحن بردى الحديثة"].Id;
        var beurre   = sups["ألبان الغوطة"].Id;
        var choco    = sups["شركة كاكاو الشام"].Id;

        // (sku, name, unit, categoryId, supplierId, cost, sale, reorderLevel)
        var defs = new (string Sku, string Name, string Unit, Guid CatId, Guid SupId, decimal Cost, decimal Sale, int Reorder)[]
        {
            // Viennoiseries
            ("VN-001", "كرواسون بالجبنة",                 "قطعة", viennoiseries, moulins, 0.85m,  2.50m, 20),
            ("VN-002", "كرواسون بالشوكولا",               "قطعة", viennoiseries, moulins, 0.95m,  2.75m, 20),
            ("VN-003", "كرواسون بالزعتر",                 "قطعة", viennoiseries, moulins, 0.75m,  2.25m, 15),
            ("VN-004", "معروك بالتمر",                    "قطعة", viennoiseries, moulins, 1.10m,  3.25m, 10),
            // Cakes & Tarts
            ("CT-001", "قطعة كاتو شوكولا",                "قطعة", cakesTarts, beurre, 2.50m,  6.00m,  8),
            ("CT-002", "قطعة كاتو فانيلا وفواكه",         "قطعة", cakesTarts, beurre, 2.80m,  6.50m,  8),
            ("CT-003", "تارت بالفراولة",                  "قطعة", cakesTarts, beurre, 3.00m,  7.50m,  5),
            ("CT-004", "تشيز كيك لوتس",                   "قطعة", cakesTarts, beurre, 3.25m,  8.00m,  6),
            ("CT-005", "قالب كاتو شوكولا",                "قطعة", cakesTarts, beurre, 12.00m, 35.00m, 2),
            // Breads
            ("BR-001", "خبز صمون",                        "قطعة", breads, moulins, 0.35m,  1.00m, 30),
            ("BR-002", "خبز فرنسي",                       "قطعة", breads, moulins, 0.60m,  1.80m, 20),
            ("BR-003", "خبز نخالة",                       "قطعة", breads, moulins, 0.70m,  2.00m, 15),
            ("BR-004", "خبز بالحليب",                     "قطعة", breads, moulins, 0.85m,  2.50m, 12),
            // Petit Fours
            ("PF-001", "علبة برازق شامية",                "علبة", petitFours, choco, 4.00m, 11.00m, 10),
            ("PF-002", "تشكيلة معمول",                    "طقم",  petitFours, choco, 5.00m, 14.00m,  8),
            ("PF-003", "علبة غريبة شامية",                "علبة", petitFours, choco, 3.50m, 10.00m,  8),
            ("PF-004", "علبة بيتيفور مشكل",               "علبة", petitFours, choco, 4.50m, 12.00m, 10),
            ("PF-005", "عش البلبل بالفستق",               "قطعة", petitFours, choco, 1.50m,  4.00m, 12),
            // Cookies & Biscuits
            ("CB-001", "علبة بسكويت باليانسون",           "علبة", cookies, beurre, 2.50m,  7.00m, 10),
            ("CB-002", "علبة كوكيز بالشوكولا",            "علبة", cookies, beurre, 3.00m,  8.00m, 12),
            ("CB-003", "سابليه بالمربى",                  "قطعة", cookies, beurre, 0.60m,  1.75m, 20),
            // Seasonal Specials
            ("SS-001", "علبة معمول العيد بالتمر",          "علبة", seasonal, choco, 4.50m, 13.00m, 6),
            ("SS-002", "معروك رمضان بالقشطة",              "قطعة", seasonal, choco, 1.40m,  4.00m, 8),
            ("SS-003", "علبة قطايف بالجوز",                "علبة", seasonal, choco, 4.00m, 11.00m, 6),
        };

        var englishNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["VN-001"] = "Cheese Croissant",
            ["VN-002"] = "Chocolate Croissant",
            ["VN-003"] = "Zaatar Croissant",
            ["VN-004"] = "Date Maarouk",
            ["CT-001"] = "Chocolate Gateau Slice",
            ["CT-002"] = "Vanilla Fruit Gateau Slice",
            ["CT-003"] = "Strawberry Tart",
            ["CT-004"] = "Lotus Cheesecake",
            ["CT-005"] = "Whole Chocolate Gateau",
            ["BR-001"] = "Samoon Bread",
            ["BR-002"] = "French Bread",
            ["BR-003"] = "Bran Bread",
            ["BR-004"] = "Milk Bread",
            ["PF-001"] = "Damascene Barazek Box",
            ["PF-002"] = "Assorted Maamoul",
            ["PF-003"] = "Damascene Ghraybeh Box",
            ["PF-004"] = "Assorted Petit Four Box",
            ["PF-005"] = "Pistachio Osh El Bulbul",
            ["CB-001"] = "Anise Biscuit Box",
            ["CB-002"] = "Chocolate Cookie Box",
            ["CB-003"] = "Jam Sable Biscuit",
            ["SS-001"] = "Eid Date Maamoul Box",
            ["SS-002"] = "Ramadan Cream Maarouk",
            ["SS-003"] = "Walnut Qatayef Box"
        };

        var result = new Dictionary<string, AppProduct>();
        foreach (var d in defs)
        {
            var unitEn = d.Unit switch
            {
                "علبة" => "box",
                "طقم" => "set",
                _ => "piece"
            };
            var p = await _productManager.CreateAsync(
                d.CatId, d.Name, englishNames[d.Sku], d.Sku, d.Unit, unitEn, d.SupId,
                costPrice: d.Cost, salePrice: d.Sale,
                currency: "USD", reorderLevel: d.Reorder,
                shelfLifeDays: ShelfLifeBySku.TryGetValue(d.Sku, out var shelf) ? shelf : null);
            await _productRepo.InsertAsync(p, autoSave: true);
            result[d.Sku] = p;
        }

        _logger.LogInformation("[Seed] Seeded {Count} products.", result.Count);
        return result;
    }

    /// <summary>
    /// Idempotent backfill: seeded products that pre-date the shelf-life column (or a
    /// re-run after the Products table already existed) get their ShelfLifeDays set
    /// if still null. Products the admin already marked perishable are not touched.
    /// </summary>
    private async Task BackfillShelfLifeAsync(Dictionary<string, AppProduct> products)
    {
        var updated = 0;
        foreach (var (sku, product) in products)
        {
            if (product.ShelfLifeDays == null && ShelfLifeBySku.TryGetValue(sku, out var shelf))
            {
                product.SetShelfLifeDays(shelf);
                await _productRepo.UpdateAsync(product, autoSave: true);
                updated++;
            }
        }

        if (updated > 0)
        {
            _logger.LogInformation("[Seed] Backfilled shelf life on {Count} products.", updated);
        }
    }

    // ─────────────────────────── Branches ─────────────────────────────

    private async Task<Dictionary<string, AppBranch>> SeedBranchesAsync()
    {
        if (await _branchRepo.CountAsync() > 0)
        {
            _logger.LogInformation("[Seed] Branches already exist — skipping.");
            var existing = await _branchRepo.GetListAsync();
            return existing.ToDictionary(b => b.NameAr);
        }

        var englishBranches = new Dictionary<string, (string Name, string Address)>
        {
            ["فرع المزة"] = ("Mazzeh Branch", "Mazzeh Highway, beside Al Jalaa City, Damascus"),
            ["فرع المالكي"] = ("Malki Branch", "Abdel Moneim Riad Street, Malki, Damascus"),
            ["فرع أبو رمانة"] = ("Abu Rummaneh Branch", "Nazem Pasha Avenue, Abu Rummaneh, Damascus"),
            ["فرع كفرسوسة"] = ("Kafr Sousa Branch", "Al Baraem Street, Kafr Sousa, Damascus"),
            ["فرع الشعلان"] = ("Shaalan Branch", "Al Hamra Street, Shaalan, Damascus"),
            ["فرع مشروع دمر"] = ("Dummar Project Branch", "Sixth Island, Dummar Project, Damascus"),
            ["فرع باب توما"] = ("Bab Touma Branch", "Bab Touma Square, Old Damascus"),
            ["فرع جرمانا"] = ("Jaramana Branch", "Municipality Street, Jaramana, Rural Damascus"),
            [GraduationSeedData.MainKitchenName] = ("Central Kitchen", "Industrial Area, Southern Damascus Entrance")
        };

        var result = new Dictionary<string, AppBranch>();
        foreach (var spec in GraduationSeedData.RetailBranches.Append(GraduationSeedData.MainKitchen))
        {
            var manager = await _userManager.FindByNameAsync(spec.ManagerUserName)
                ?? throw new InvalidOperationException(
                    $"Presentation manager '{spec.ManagerUserName}' must be created before branches.");

            var english = englishBranches[spec.Name];
            var branch = await _branchManager.CreateAsync(
                spec.Name,
                english.Name,
                spec.Address,
                english.Address,
                spec.Phone,
                spec.Email,
                manager.Id,
                isActive: true,
                branchType: spec.BranchType);

            await _branchRepo.InsertAsync(branch, autoSave: true);
            result[spec.Name] = branch;

            if (spec.CashierUserName != null)
            {
                await EnsureCashierAssignmentAsync(spec.CashierUserName, branch.Id);
            }
        }

        _logger.LogInformation(
            "[Seed] Created {Count} presentation sites: {RetailCount} sales branches and one central kitchen.",
            result.Count,
            GraduationSeedData.RetailBranches.Count);
        return result;
    }

    private async Task EnsureCashierAssignmentAsync(string cashierUserName, Guid branchId)
    {
        var cashier = await _userManager.FindByNameAsync(cashierUserName)
            ?? throw new InvalidOperationException(
                $"Presentation cashier '{cashierUserName}' must be created before branches.");

        var desiredValue = branchId.ToString();
        var claims = await _userManager.GetClaimsAsync(cashier);
        var assignmentClaims = claims
            .Where(c => c.Type == CashierClaimTypes.AssignedBranchId)
            .ToList();

        if (assignmentClaims.Count == 1 && assignmentClaims[0].Value == desiredValue)
        {
            return;
        }

        foreach (var claim in assignmentClaims)
        {
            EnsureIdentitySucceeded(
                await _userManager.RemoveClaimAsync(cashier, claim),
                $"remove the old branch assignment from '{cashierUserName}'");
        }

        EnsureIdentitySucceeded(
            await _userManager.AddClaimAsync(
                cashier,
                new Claim(CashierClaimTypes.AssignedBranchId, desiredValue)),
            $"assign '{cashierUserName}' to branch '{branchId}'");
    }

    private static void EnsureIdentitySucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Failed to {operation}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }

    // ─────────────────────────── Branch Inventory ─────────────────────

    private async Task SeedInventoryAsync(
        Dictionary<string, AppProduct> products,
        Dictionary<string, AppBranch> branches)
    {
        if (await _inventoryRepo.CountAsync() > 0)
        {
            _logger.LogInformation("[Seed] Branch inventory already exists — skipping.");
            return;
        }

        if (!branches.TryGetValue(GraduationSeedData.MainKitchenName, out var mainKitchen))
        {
            _logger.LogWarning(
                "[Seed] Inventory creation skipped because the central kitchen was not found. Existing branches: {Names}.",
                string.Join(", ", branches.Keys));
            return;
        }

        var rows = new List<AppBranchInventory>();
        var kitchenQuantities = new Dictionary<string, int>
        {
            ["VN-001"]=90, ["VN-002"]=85, ["VN-003"]=70, ["VN-004"]=60,
            ["CT-001"]=50, ["CT-002"]=55, ["CT-003"]=48, ["CT-004"]=65, ["CT-005"]=42,
            ["BR-001"]=120,["BR-002"]=75, ["BR-003"]=60, ["BR-004"]=55,
            ["PF-001"]=80, ["PF-002"]=70, ["PF-003"]=65, ["PF-004"]=90, ["PF-005"]=45,
            ["CB-001"]=85, ["CB-002"]=80, ["CB-003"]=70,
            ["SS-001"]=50, ["SS-002"]=45, ["SS-003"]=60,
        };

        foreach (var (sku, p) in products)
        {
            var qty = kitchenQuantities.GetValueOrDefault(sku, 60);
            var inv = await _inventoryManager.InitializeAsync(mainKitchen.Id, p.Id, qty, 20, 180);
            rows.Add(inv);
            await _inventoryRepo.InsertAsync(inv, autoSave: true);
        }

        // Each retail branch gets a different but deterministic sales profile. A handful of
        // deliberate exceptions make the dashboards useful immediately: low stock, an outage,
        // excess stock and several products that have not moved recently.
        var productList = products.Values.OrderBy(p => p.SKU).ToList();
        var retailRows = new Dictionary<(string BranchName, string Sku), AppBranchInventory>();
        for (var branchIndex = 0; branchIndex < GraduationSeedData.RetailBranches.Count; branchIndex++)
        {
            var branchSpec = GraduationSeedData.RetailBranches[branchIndex];
            if (!branches.TryGetValue(branchSpec.Name, out var branch))
            {
                continue;
            }

            for (var productIndex = 0; productIndex < productList.Count; productIndex++)
            {
                var product = productList[productIndex];
                var minimum = product.SKU.StartsWith("BR-", StringComparison.Ordinal) ? 12
                    : product.SKU.StartsWith("VN-", StringComparison.Ordinal) ? 9
                    : product.SalePrice >= 20m ? 2
                    : 5;
                var quantity = minimum + 4 + ((branchIndex * 5 + productIndex * 3) % 17);

                quantity = (branchIndex, product.SKU) switch
                {
                    (0, "VN-001") => 3,
                    (0, "BR-001") => 2,
                    (1, "CT-005") => 0,
                    (2, "VN-004") => 1,
                    (3, "PF-002") => 0,
                    (4, "CB-001") => 112,
                    (5, "CT-004") => 2,
                    (6, "BR-003") => 4,
                    (7, "VN-002") => 3,
                    _ => quantity
                };

                var inventory = await _inventoryManager.InitializeAsync(
                    branch.Id,
                    product.Id,
                    quantity,
                    minimum,
                    Math.Max(50, minimum * 8));
                rows.Add(inventory);
                retailRows[(branchSpec.Name, product.SKU)] = inventory;
                await _inventoryRepo.InsertAsync(inventory, autoSave: true);
            }
        }

        var dbContext = await _dbContextProvider.GetDbContextAsync();
        await dbContext.SaveChangesAsync();
        var now = DateTime.UtcNow;
        var kitchenIds = rows
            .Where(r => r.BranchId == mainKitchen.Id)
            .Select(r => r.Id)
            .ToList();
        await dbContext.Set<AppBranchInventory>()
            .Where(x => kitchenIds.Contains(x.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastRestockedDate, now.AddDays(-2)));

        var retailIds = retailRows.Values.Select(x => x.Id).ToList();
        await dbContext.Set<AppBranchInventory>()
            .Where(x => retailIds.Contains(x.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastRestockedDate, now.AddDays(-4))
                .SetProperty(x => x.LastSoldDate, now.AddDays(-1)));

        var slowMovingKeys = new[]
        {
            (GraduationSeedData.RetailBranches[0].Name, "SS-001"),
            (GraduationSeedData.RetailBranches[1].Name, "SS-002"),
            (GraduationSeedData.RetailBranches[5].Name, "PF-005"),
            (GraduationSeedData.RetailBranches[7].Name, "SS-003")
        };
        var slowMovingIds = slowMovingKeys
            .Where(retailRows.ContainsKey)
            .Select(key => retailRows[key].Id)
            .ToList();
        await dbContext.Set<AppBranchInventory>()
            .Where(x => slowMovingIds.Contains(x.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastRestockedDate, now.AddDays(-48))
                .SetProperty(x => x.LastSoldDate, now.AddDays(-41)));

        _logger.LogInformation(
            "[Seed] Created {Count} inventory balances for {Branches} sites and {Products} products.",
            rows.Count, branches.Count, products.Count);
    }

    // ─────────────────────────── Stock Batches ────────────────────────

    /// <summary>
    /// Splits each perishable product's on-hand quantity into 1–3 opening-balance batches with
    /// staggered expiries — the oldest batch expires within 1–2 days so the ExpiringSoon
    /// rule demos instantly, the freshest gets the full shelf life. Each RETAIL branch
    /// (everything except the central warehouse) additionally gets at least one batch
    /// that is ALREADY expired with remaining quantity, so the ExpiredStock rule has
    /// waste to flag out of the box. Deterministic (fixed-seed Random, rows processed
    /// in a stable order) and idempotent (skipped entirely once any batch exists).
    /// </summary>
    private async Task SeedStockBatchesAsync(
        Dictionary<string, AppProduct> products,
        Dictionary<string, AppBranch> branches)
    {
        if (await _batchRepo.AnyAsync())
        {
            _logger.LogInformation("[Seed] Stock batches already exist — skipping.");
            return;
        }

        var productById = products.Values.ToDictionary(p => p.Id);

        // Every sales branch gets an already-expired slice so expiry decisions and
        // write-off actions have meaningful work immediately.
        var retailBranchIds = branches.Values
            .Where(b => b.BranchType == BranchTypes.SalesBranch)
            .Select(b => b.Id)
            .ToHashSet();
        var expiredEnsured = new HashSet<Guid>();

        // Stable order (branch, then SKU) so the fixed-seed Random yields the same
        // batches on every fresh database.
        var invRows = (await _inventoryRepo.GetListAsync())
            .Where(r => r.QuantityOnHand > 0
                        && productById.TryGetValue(r.ProductId, out var p)
                        && p.ShelfLifeDays != null)
            .OrderBy(r => r.BranchId)
            .ThenBy(r => productById[r.ProductId].SKU)
            .ToList();

        var rng = new Random(20260730);
        var today = DateTime.UtcNow.Date;
        var created = 0;

        foreach (var row in invRows)
        {
            var product = productById[row.ProductId];
            var shelf = product.ShelfLifeDays!.Value;
            var qty = row.QuantityOnHand;

            // 1–3 batches depending on how much stock there is.
            var batchCount = qty <= 5 ? 1 : qty <= 15 ? 2 : 3;

            // Expiry offsets, oldest → freshest. The oldest slice expires in 1–2 days
            // (capped by shelf life); the freshest got delivered today.
            var offsets = batchCount switch
            {
                1 => new[] { shelf },
                2 => new[] { Math.Min(1 + rng.Next(0, 2), shelf), shelf },
                _ => new[]
                {
                    Math.Min(1 + rng.Next(0, 2), shelf),
                    Math.Min(Math.Max(3, shelf / 2), shelf),
                    shelf
                }
            };

            // The first multi-batch perishable row of each retail branch
            // gets its oldest slice backdated to two days PAST expiry (only a slice —
            // batchCount >= 2 keeps the rest of the stock sellable).
            if (retailBranchIds.Contains(row.BranchId)
                && !expiredEnsured.Contains(row.BranchId)
                && batchCount >= 2)
            {
                offsets[0] = -2;
                expiredEnsured.Add(row.BranchId);
            }

            // Quantity split: ~30% / ~30% / remainder (older slices smaller).
            var quantities = batchCount switch
            {
                1 => new[] { qty },
                2 => new[] { Math.Max(1, qty * 4 / 10), 0 },
                _ => new[] { Math.Max(1, qty * 3 / 10), Math.Max(1, qty * 3 / 10), 0 }
            };
            quantities[^1] = qty - quantities.Take(batchCount - 1).Sum();

            for (var i = 0; i < batchCount; i++)
            {
                await _stockBatchManager.CreateAsync(
                    row.BranchId,
                    row.ProductId,
                    quantities[i],
                    today.AddDays(offsets[i]),
                    StockBatchSourceTypes.Adjustment,
                    sourceId: null,
                    autoSave: true);
                created++;
            }
        }

        _logger.LogInformation(
            "[Seed] Seeded {Count} stock batches across {Rows} perishable inventory rows " +
            "({Expired} sales branches include an expired opening-balance slice).",
            created, invRows.Count, expiredEnsured.Count);
    }

    // ─────────────────────────── Demo Rules ───────────────────────────

    /// <summary>
    /// Phase 2/3 demo rules, seeded per-type so they also land on databases where the
    /// original rule bank (from IntelligenceDataSeedContributor) already exists.
    /// </summary>
    private async Task SeedDemoRulesAsync()
    {
        await UpgradeLegacyDemoRuleWordingAsync();

        if (!await _ruleRepo.AnyAsync(r => r.RuleType == InventoryRuleTypes.DaysOfCover))
        {
            var rule = await _ruleManager.CreateAsync(
                ruleName: "المخزون بيكفي أقل من 4 أيام",
                ruleType: InventoryRuleTypes.DaysOfCover,
                productId: null,
                branchId: null,
                thresholdValue: 4,
                thresholdDays: null,
                suggestedAction: "اطلب كمية جديدة قبل ما يخلص المخزون؛ الكمية الحالية بتكفي أقل من 4 أيام.",
                priority: 3,
                isActive: true,
                actionMode: RuleActionModes.SuggestOnly);
            await _ruleRepo.InsertAsync(rule, autoSave: true);
            _logger.LogInformation("[Seed] Seeded global DaysOfCover demo rule.");
        }

        if (!await _ruleRepo.AnyAsync(r => r.RuleType == InventoryRuleTypes.ExpiringSoon))
        {
            var rule = await _ruleManager.CreateAsync(
                ruleName: "الصلاحية بتنتهي خلال 3 أيام",
                ruleType: InventoryRuleTypes.ExpiringSoon,
                productId: null,
                branchId: null,
                thresholdValue: null,
                thresholdDays: 3,
                suggestedAction: "اعمل خصم أو انقل الكمية لفرع أسرع بيع قبل ما تنتهي صلاحيتها.",
                priority: 5,
                isActive: true,
                actionMode: RuleActionModes.SuggestOnly);
            await _ruleRepo.InsertAsync(rule, autoSave: true);
            _logger.LogInformation("[Seed] Seeded global ExpiringSoon demo rule.");
        }

        if (!await _ruleRepo.AnyAsync(r => r.RuleType == InventoryRuleTypes.ExpiredStock))
        {
            // SuggestOnly ON PURPOSE: the write-off is destructive, so the decision is
            // never auto-executed — a human triggers it from the decision log.
            var rule = await _ruleManager.CreateAsync(
                ruleName: "شطب كمية منتهية الصلاحية",
                ruleType: InventoryRuleTypes.ExpiredStock,
                productId: null,
                branchId: null,
                thresholdValue: null,
                thresholdDays: null,
                suggestedAction: "اشطب الكمية المنتهية من سجل القرارات حتى يضل الجرد صحيح.",
                priority: 6,
                isActive: true,
                actionMode: RuleActionModes.SuggestOnly);
            await _ruleRepo.InsertAsync(rule, autoSave: true);
            _logger.LogInformation("[Seed] Seeded global ExpiredStock demo rule.");
        }
    }

    private async Task UpgradeLegacyDemoRuleWordingAsync()
    {
        var translations = new[]
        {
            new LegacyDemoRuleTranslation(
                InventoryRuleTypes.DaysOfCover, 4, null, 3,
                "خطر تغطية المخزون (4 أيام)",
                "أعِد الطلب قبل نفاد المخزون — تبقّى أقل من 4 أيام من الطلب",
                "المخزون بيكفي أقل من 4 أيام",
                "اطلب كمية جديدة قبل ما يخلص المخزون؛ الكمية الحالية بتكفي أقل من 4 أيام."),
            new LegacyDemoRuleTranslation(
                InventoryRuleTypes.ExpiringSoon, null, 3, 5,
                "قريب الانتهاء (3 أيام)",
                "طبّق خصمًا أو انقل المخزون إلى فرع أسرع حركة قبل انتهاء صلاحيته",
                "الصلاحية بتنتهي خلال 3 أيام",
                "اعمل خصم أو انقل الكمية لفرع أسرع بيع قبل ما تنتهي صلاحيتها."),
            new LegacyDemoRuleTranslation(
                InventoryRuleTypes.ExpiredStock, null, null, 6,
                "شطب المخزون منتهي الصلاحية",
                "اشطب المخزون منتهي الصلاحية ليبقى الجرد دقيقًا — نفّذ ذلك من سجل القرارات",
                "شطب كمية منتهية الصلاحية",
                "اشطب الكمية المنتهية من سجل القرارات حتى يضل الجرد صحيح.")
        };

        var rules = await _ruleRepo.GetListAsync(r =>
            r.ProductId == null
            && r.BranchId == null
            && (r.RuleType == InventoryRuleTypes.DaysOfCover
                || r.RuleType == InventoryRuleTypes.ExpiringSoon
                || r.RuleType == InventoryRuleTypes.ExpiredStock));

        foreach (var rule in rules)
        {
            foreach (var translation in translations)
            {
                if (!translation.Matches(rule))
                {
                    continue;
                }

                var changed = false;
                if (rule.RuleName == translation.LegacyRuleName)
                {
                    rule.SetRuleName(translation.ArabicRuleName);
                    changed = true;
                }

                if (rule.SuggestedAction == translation.LegacySuggestedAction)
                {
                    rule.SetSuggestedAction(translation.ArabicSuggestedAction);
                    changed = true;
                }

                if (changed)
                {
                    await _ruleRepo.UpdateAsync(rule, autoSave: true);
                }

                break;
            }
        }
    }

    private sealed record LegacyDemoRuleTranslation(
        string RuleType,
        int? ThresholdValue,
        int? ThresholdDays,
        int Priority,
        string LegacyRuleName,
        string LegacySuggestedAction,
        string ArabicRuleName,
        string ArabicSuggestedAction)
    {
        public bool Matches(AppInventoryRule rule)
            => rule.RuleType == RuleType
               && rule.ThresholdValue == ThresholdValue
               && rule.ThresholdDays == ThresholdDays
               && rule.Priority == Priority
               && rule.IsActive
               && rule.ActionMode == RuleActionModes.SuggestOnly
               && rule.CreatorId == null
               && rule.LastModificationTime == null
               && rule.LastModifierId == null
               && (rule.RuleName == LegacyRuleName || rule.RuleName == ArabicRuleName)
               && (rule.SuggestedAction == LegacySuggestedAction
                   || rule.SuggestedAction == ArabicSuggestedAction);
    }
}
