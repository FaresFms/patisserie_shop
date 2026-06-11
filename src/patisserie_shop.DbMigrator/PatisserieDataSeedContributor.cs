using System;
using System.Collections.Generic;
using System.Linq;
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
using Microsoft.Extensions.Logging;
using patisserie_shop.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
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
        ["VN-001"] = 2, ["VN-002"] = 2, ["VN-003"] = 3, ["VN-004"] = 3,
        ["CT-001"] = 3, ["CT-002"] = 4, ["CT-003"] = 3, ["CT-004"] = 4, ["CT-005"] = 3,
        ["BR-001"] = 2, ["BR-002"] = 3, ["BR-003"] = 3, ["BR-004"] = 2,
        ["PF-001"] = 7, ["PF-002"] = 7, ["PF-003"] = 7, ["PF-004"] = 10, ["PF-005"] = 7,
        ["CB-001"] = 14, ["CB-002"] = 10, ["CB-003"] = 14,
        ["SS-001"] = 5, ["SS-002"] = 5, ["SS-003"] = 90,
    };

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        var categories = await SeedCategoriesAsync();
        var suppliers  = await SeedSuppliersAsync();
        var products   = await SeedProductsAsync(categories, suppliers);
        await BackfillShelfLifeAsync(products);
        var branches   = await SeedBranchesAsync();
        await SeedInventoryAsync(products, branches);
        await SeedStockBatchesAsync(products);
        await SeedDemoRulesAsync();
    }

    // ─────────────────────────── Categories ───────────────────────────

    private async Task<Dictionary<string, AppCategory>> SeedCategoriesAsync()
    {
        if (await _categoryRepo.CountAsync() > 0)
        {
            _logger.LogInformation("[Seed] Categories already exist — skipping.");
            var existing = await _categoryRepo.GetListAsync();
            return existing.ToDictionary(c => c.Name);
        }

        var defs = new[]
        {
            ("Viennoiseries",       "Laminated pastries like croissants and pain au chocolat"),
            ("Cakes & Tarts",       "Full-size and individual cakes, fruit tarts"),
            ("Breads",              "Daily artisan breads and baguettes"),
            ("Petit Fours",         "Bite-sized pastries, macarons, mini éclairs"),
            ("Cookies & Biscuits",  "Butter cookies, sablés, biscotti"),
            ("Seasonal Specials",   "Rotating seasonal and holiday items"),
        };

        var result = new Dictionary<string, AppCategory>();
        foreach (var (name, desc) in defs)
        {
            var cat = await _categoryManager.CreateAsync(name, desc);
            await _categoryRepo.InsertAsync(cat, autoSave: true);
            result[name] = cat;
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
            ("MoulinsFlour Co.",      "Jean Dupont",   "+33-1-4567-8901", "orders@moulinsflour.com",  "12 Rue des Moulins, Paris"),
            ("BeurreGold Dairy",      "Marie Laurent", "+33-1-5678-9012", "supply@beurregold.com",    "45 Avenue du Lait, Lyon"),
            ("ChocoPremium Imports",  "Luca Rossi",    "+33-1-6789-0123", "orders@chocopremium.eu",   "8 Boulevard du Cacao, Marseille"),
            ("PackRight Solutions",   "Sarah Martin",  "+33-1-7890-1234", "info@packright.com",       "22 Rue de l'Emballage, Toulouse"),
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

        var viennoiseries = cats["Viennoiseries"].Id;
        var cakesTarts    = cats["Cakes & Tarts"].Id;
        var breads        = cats["Breads"].Id;
        var petitFours    = cats["Petit Fours"].Id;
        var cookies       = cats["Cookies & Biscuits"].Id;
        var seasonal      = cats["Seasonal Specials"].Id;

        var moulins  = sups["MoulinsFlour Co."].Id;
        var beurre   = sups["BeurreGold Dairy"].Id;
        var choco    = sups["ChocoPremium Imports"].Id;

        // (sku, name, unit, categoryId, supplierId, cost, sale, reorderLevel)
        var defs = new (string Sku, string Name, string Unit, Guid CatId, Guid SupId, decimal Cost, decimal Sale, int Reorder)[]
        {
            // Viennoiseries
            ("VN-001", "Classic Butter Croissant",       "Piece", viennoiseries, moulins, 0.85m,  2.50m, 20),
            ("VN-002", "Pain au Chocolat",               "Piece", viennoiseries, moulins, 0.95m,  2.75m, 20),
            ("VN-003", "Almond Croissant",               "Piece", viennoiseries, moulins, 1.20m,  3.50m, 15),
            ("VN-004", "Brioche Loaf",                   "Piece", viennoiseries, moulins, 1.50m,  4.00m, 10),
            // Cakes & Tarts
            ("CT-001", "Classic Strawberry Tart",        "Piece", cakesTarts, beurre, 3.50m,  8.50m,  5),
            ("CT-002", "Opera Cake Slice",               "Piece", cakesTarts, beurre, 2.80m,  6.50m,  8),
            ("CT-003", "Lemon Meringue Tart",            "Piece", cakesTarts, beurre, 3.00m,  7.50m,  5),
            ("CT-004", "Chocolate Fondant",              "Piece", cakesTarts, beurre, 2.50m,  6.00m,  8),
            ("CT-005", "Whole Celebration Cake",         "Piece", cakesTarts, beurre, 12.00m, 35.00m, 2),
            // Breads
            ("BR-001", "Traditional Baguette",           "Piece", breads, moulins, 0.60m,  1.80m, 30),
            ("BR-002", "Sourdough Boule",                "Piece", breads, moulins, 1.20m,  3.50m, 10),
            ("BR-003", "Multigrain Loaf",                "Piece", breads, moulins, 1.40m,  4.00m,  8),
            ("BR-004", "Olive Focaccia",                 "Piece", breads, moulins, 1.60m,  4.50m,  6),
            // Petit Fours
            ("PF-001", "Assorted Macarons Box (12pc)",   "Box",   petitFours, choco, 6.00m, 18.00m, 10),
            ("PF-002", "Mini Éclair Set (6pc)",          "Set",   petitFours, choco, 4.50m, 12.00m,  8),
            ("PF-003", "Cannelés (4pc)",                 "Box",   petitFours, choco, 3.00m,  8.00m,  8),
            ("PF-004", "Madeleines (6pc)",               "Box",   petitFours, choco, 2.50m,  6.50m, 10),
            ("PF-005", "Profiterole Tower",              "Piece", petitFours, choco, 5.00m, 14.00m,  4),
            // Cookies & Biscuits
            ("CB-001", "Butter Sablé Tin",              "Box",   cookies, beurre, 3.50m,  9.00m, 10),
            ("CB-002", "Double Chocolate Cookies (6pc)", "Box",   cookies, beurre, 2.80m,  7.00m, 12),
            ("CB-003", "Almond Biscotti Bag",            "Piece", cookies, beurre, 2.00m,  5.50m, 10),
            // Seasonal Specials
            ("SS-001", "Galette des Rois",               "Piece", seasonal, choco, 5.00m, 15.00m, 3),
            ("SS-002", "Bûche de Noël (serves 8)",      "Piece", seasonal, choco, 8.00m, 25.00m, 2),
            ("SS-003", "Easter Chocolate Egg",           "Piece", seasonal, choco, 4.50m, 12.00m, 5),
        };

        var result = new Dictionary<string, AppProduct>();
        foreach (var d in defs)
        {
            var p = await _productManager.CreateAsync(
                d.CatId, d.Name, d.Sku, d.Unit, d.SupId,
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
            return existing.ToDictionary(b => b.Name);
        }

        Guid? managerUserId = null;
        var managerUser = await _userManager.FindByNameAsync(IdentityDataSeedContributor.BranchManagerUserName);
        if (managerUser != null)
        {
            managerUserId = managerUser.Id;
        }

        var defs = new[]
        {
            ("Central Kitchen & Warehouse",
             "1 Rue de la Pâtisserie, Paris 75001", "+33-1-0000-0001", "warehouse@patisserie.com", (Guid?)null),

            ("Main Street Boutique",
             "88 Boulevard Haussmann, Paris 75008", "+33-1-0000-0002", "mainstreet@patisserie.com", managerUserId),

            ("Riverside Café",
             "15 Quai de la Tournelle, Paris 75005", "+33-1-0000-0003", "riverside@patisserie.com", (Guid?)null),
        };

        var result = new Dictionary<string, AppBranch>();
        foreach (var (name, address, phone, email, mgr) in defs)
        {
            var b = await _branchManager.CreateAsync(name, address, phone, email, mgr);
            await _branchRepo.InsertAsync(b, autoSave: true);
            result[name] = b;
        }

        _logger.LogInformation("[Seed] Seeded {Count} branches (manager.demo assigned: {Assigned}).",
            result.Count, managerUserId.HasValue ? "yes" : "no");
        return result;
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

        // Guard: if the expected branch names don't exist (user created custom branches
        // via the UI before the seeder ran), skip inventory seeding with a clear message.
        if (!branches.ContainsKey("Central Kitchen & Warehouse") ||
            !branches.ContainsKey("Main Street Boutique") ||
            !branches.ContainsKey("Riverside Café"))
        {
            _logger.LogWarning(
                "[Seed] Inventory seeding skipped — expected branch names not found. " +
                "Existing branches: {Names}. " +
                "To seed inventory, delete the existing branches and re-run the migrator.",
                string.Join(", ", branches.Keys));
            return;
        }

        var warehouse  = branches["Central Kitchen & Warehouse"];
        var mainStreet = branches["Main Street Boutique"];
        var riverside  = branches["Riverside Café"];

        var rows = new List<AppBranchInventory>();

        // ── Central Kitchen (high stock, source branch) ──
        var warehouseQty = new Dictionary<string, int>
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
            var qty = warehouseQty.GetValueOrDefault(sku, 60);
            var inv = await _inventoryManager.InitializeAsync(warehouse.Id, p.Id, qty, 20, 150);
            rows.Add(inv);
            await _inventoryRepo.InsertAsync(inv, autoSave: true);
        }

        // ── Main Street Boutique (mixed — some low) ──
        var mainQty = new Dictionary<string, (int Qty, int Min)>
        {
            ["VN-001"]=(3,  10), // LOW
            ["VN-002"]=(4,  10), // LOW
            ["VN-003"]=(12,  8), // healthy
            ["VN-004"]=(1,   5), // CRITICAL
            ["CT-001"]=(8,   3), // healthy
            ["CT-002"]=(15,  5), // healthy
            ["CT-003"]=(10,  5), // healthy
            ["CT-004"]=(9,   5), // healthy
            ["CT-005"]=(4,   2), // healthy
            ["BR-001"]=(2,  15), // LOW
            ["BR-002"]=(7,   5), // healthy
            ["BR-003"]=(11,  5), // healthy
            ["BR-004"]=(9,   5), // healthy
            ["PF-001"]=(25,  5), // healthy
            ["PF-002"]=(0,   5), // OUT OF STOCK
            ["PF-003"]=(14,  5), // healthy
            ["PF-004"]=(18,  5), // healthy
            ["PF-005"]=(8,   5), // healthy
            ["CB-001"]=(12,  5), // healthy
            ["CB-002"]=(16,  5), // healthy
            ["CB-003"]=(10,  5), // healthy
            ["SS-001"]=(6,   3), // healthy (but dead stock by date)
            ["SS-002"]=(4,   2), // healthy (but dead stock by date)
            ["SS-003"]=(8,   5), // healthy (but dead stock by date)
        };
        var mainRows = new Dictionary<string, AppBranchInventory>();
        foreach (var (sku, p) in products)
        {
            var (qty, min) = mainQty.GetValueOrDefault(sku, (10, 5));
            var inv = await _inventoryManager.InitializeAsync(mainStreet.Id, p.Id, qty, min, 50);
            rows.Add(inv);
            mainRows[sku] = inv;
            await _inventoryRepo.InsertAsync(inv, autoSave: true);
        }

        // ── Riverside Café (mostly healthy, a few issues) ──
        var riversideQty = new Dictionary<string, (int Qty, int Min)>
        {
            ["VN-001"]=(18, 10), // healthy
            ["VN-002"]=(14, 10), // healthy
            ["VN-003"]=(12,  5), // healthy
            ["VN-004"]=(8,   5), // healthy
            ["CT-001"]=(10,  5), // healthy
            ["CT-002"]=(14,  5), // healthy
            ["CT-003"]=(11,  5), // healthy
            ["CT-004"]=(2,   5), // LOW
            ["CT-005"]=(0,   1), // OUT OF STOCK
            ["BR-001"]=(22, 15), // healthy
            ["BR-002"]=(15,  5), // healthy
            ["BR-003"]=(12,  5), // healthy
            ["BR-004"]=(10,  5), // healthy
            ["PF-001"]=(20,  5), // healthy
            ["PF-002"]=(16,  5), // healthy
            ["PF-003"]=(13,  5), // healthy
            ["PF-004"]=(18,  5), // healthy
            ["PF-005"]=(7,   5), // healthy (but dead stock by date)
            ["CB-001"]=(110, 5), // EXCESS (over 100)
            ["CB-002"]=(20,  5), // healthy
            ["CB-003"]=(15,  5), // healthy
            ["SS-001"]=(12,  3), // healthy
            ["SS-002"]=(8,   2), // healthy
            ["SS-003"]=(14,  5), // healthy
        };
        var riversideRows = new Dictionary<string, AppBranchInventory>();
        foreach (var (sku, p) in products)
        {
            var (qty, min) = riversideQty.GetValueOrDefault(sku, (12, 5));
            var inv = await _inventoryManager.InitializeAsync(riverside.Id, p.Id, qty, min, 50);
            rows.Add(inv);
            riversideRows[sku] = inv;
            await _inventoryRepo.InsertAsync(inv, autoSave: true);
        }

        // ── Flush inserts before ExecuteUpdate ──
        var dbContext = await _dbContextProvider.GetDbContextAsync();
        await dbContext.SaveChangesAsync();

        var now = DateTime.UtcNow;

        // Central Kitchen: restocked 2 days ago, never sold to customers
        var warehouseIds = rows
            .Where(r => r.BranchId == warehouse.Id)
            .Select(r => r.Id)
            .ToList();
        await dbContext.Set<AppBranchInventory>()
            .Where(x => warehouseIds.Contains(x.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastRestockedDate, now.AddDays(-2)));

        // Main Street: normal products — restocked recently, sold recently
        var mainNormalIds = mainRows
            .Where(kv => kv.Key != "SS-001" && kv.Key != "SS-002" && kv.Key != "SS-003")
            .Select(kv => kv.Value.Id)
            .ToList();
        await dbContext.Set<AppBranchInventory>()
            .Where(x => mainNormalIds.Contains(x.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastRestockedDate, now.AddDays(-5))
                .SetProperty(x => x.LastSoldDate,      now.AddDays(-1)));

        // Main Street: dead-stock seasonal items
        var idSS001Main = mainRows["SS-001"].Id;
        var idSS002Main = mainRows["SS-002"].Id;
        var idSS003Main = mainRows["SS-003"].Id;
        await dbContext.Set<AppBranchInventory>()
            .Where(x => x.Id == idSS001Main || x.Id == idSS002Main)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastRestockedDate, now.AddDays(-50))
                .SetProperty(x => x.LastSoldDate,      now.AddDays(-45)));
        await dbContext.Set<AppBranchInventory>()
            .Where(x => x.Id == idSS003Main)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastRestockedDate, now.AddDays(-40))
                .SetProperty(x => x.LastSoldDate,      now.AddDays(-35)));

        // Riverside: normal products — restocked recently, sold recently
        var riversideNormalIds = riversideRows
            .Where(kv => kv.Key != "PF-005")
            .Select(kv => kv.Value.Id)
            .ToList();
        await dbContext.Set<AppBranchInventory>()
            .Where(x => riversideNormalIds.Contains(x.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastRestockedDate, now.AddDays(-3))
                .SetProperty(x => x.LastSoldDate,      now.AddDays(-1)));

        // Riverside: Profiterole Tower — dead stock
        var idPF005Riverside = riversideRows["PF-005"].Id;
        await dbContext.Set<AppBranchInventory>()
            .Where(x => x.Id == idPF005Riverside)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.LastRestockedDate, now.AddDays(-45))
                .SetProperty(x => x.LastSoldDate,      now.AddDays(-40)));

        _logger.LogInformation(
            "[Seed] Seeded {Count} inventory rows ({Branches} branches × {Products} products).",
            rows.Count, branches.Count, products.Count);
    }

    // ─────────────────────────── Stock Batches ────────────────────────

    /// <summary>
    /// Splits each perishable product's on-hand quantity into 1–3 "Seed" batches with
    /// staggered expiries — the oldest batch expires within 1–2 days so the ExpiringSoon
    /// rule demos instantly, the freshest gets the full shelf life. Deterministic
    /// (fixed-seed Random, rows processed in a stable order) and idempotent (skipped
    /// entirely once any batch exists).
    /// </summary>
    private async Task SeedStockBatchesAsync(Dictionary<string, AppProduct> products)
    {
        if (await _batchRepo.AnyAsync())
        {
            _logger.LogInformation("[Seed] Stock batches already exist — skipping.");
            return;
        }

        var productById = products.Values.ToDictionary(p => p.Id);

        // Stable order (branch, then SKU) so the fixed-seed Random yields the same
        // batches on every fresh database.
        var invRows = (await _inventoryRepo.GetListAsync())
            .Where(r => r.QuantityOnHand > 0
                        && productById.TryGetValue(r.ProductId, out var p)
                        && p.ShelfLifeDays != null)
            .OrderBy(r => r.BranchId)
            .ThenBy(r => productById[r.ProductId].SKU)
            .ToList();

        var rng = new Random(20260611); // fixed seed — deterministic demo data
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
                    StockBatchSourceTypes.Seed,
                    sourceId: null,
                    autoSave: true);
                created++;
            }
        }

        _logger.LogInformation(
            "[Seed] Seeded {Count} stock batches across {Rows} perishable inventory rows.",
            created, invRows.Count);
    }

    // ─────────────────────────── Demo Rules ───────────────────────────

    /// <summary>
    /// Phase 2/3 demo rules, seeded per-type so they also land on databases where the
    /// original rule bank (from IntelligenceDataSeedContributor) already exists.
    /// </summary>
    private async Task SeedDemoRulesAsync()
    {
        if (!await _ruleRepo.AnyAsync(r => r.RuleType == InventoryRuleTypes.DaysOfCover))
        {
            var rule = await _ruleManager.CreateAsync(
                ruleName: "Days of Cover Risk (4 days)",
                ruleType: InventoryRuleTypes.DaysOfCover,
                productId: null,
                branchId: null,
                thresholdValue: 4,
                thresholdDays: null,
                suggestedAction: "Reorder before stock runs out — fewer than 4 days of demand left",
                priority: 3,
                isActive: true,
                actionMode: RuleActionModes.SuggestOnly);
            await _ruleRepo.InsertAsync(rule, autoSave: true);
            _logger.LogInformation("[Seed] Seeded global DaysOfCover demo rule.");
        }

        if (!await _ruleRepo.AnyAsync(r => r.RuleType == InventoryRuleTypes.ExpiringSoon))
        {
            var rule = await _ruleManager.CreateAsync(
                ruleName: "Expiring Soon (3 days)",
                ruleType: InventoryRuleTypes.ExpiringSoon,
                productId: null,
                branchId: null,
                thresholdValue: null,
                thresholdDays: 3,
                suggestedAction: "Apply a discount or transfer the stock to a faster-moving branch before it expires",
                priority: 5,
                isActive: true,
                actionMode: RuleActionModes.SuggestOnly);
            await _ruleRepo.InsertAsync(rule, autoSave: true);
            _logger.LogInformation("[Seed] Seeded global ExpiringSoon demo rule.");
        }
    }
}
