using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Categories;
using Inventory.Entities;
using Inventory.Products;
using Inventory.StockBatches;
using Inventory.Suppliers;
using Microsoft.Extensions.Logging;
using Production;
using Production.BranchRequests;
using Production.Entities;
using Production.Formulas;
using Production.Orders;
using Production.Plans;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace patisserie_shop.DbMigrator;

public class ProductionDemoSeedContributor : IDataSeedContributor, ITransientDependency
{
    private const string DemoMarker = "[DEMO-PRODUCTION-AR]";
    private const string ShortageDemoMarker = "[DEMO-PRODUCTION-AR-SHORTAGE]";
    private const string ShortageDemoFinishedSku = "VN-002";
    private const int ShortageDemoQuantity = 340;
    private const string RawCategoryName = "مواد الإنتاج الخام";
    private const string PackagingCategoryName = "مواد التغليف";
    private const string DemoSupplierName = "مورد مواد الإنتاج التجريبي";

    private readonly CategoryManager _categoryManager;
    private readonly SupplierManager _supplierManager;
    private readonly ProductManager _productManager;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly StockBatchManager _stockBatchManager;
    private readonly ProductionFormulaManager _formulaManager;
    private readonly BranchProductionRequestManager _requestManager;
    private readonly ProductionPlanManager _planManager;
    private readonly ProductionOrderManager _orderManager;

    private readonly IRepository<AppCategory, Guid> _categoryRepo;
    private readonly IRepository<AppSupplier, Guid> _supplierRepo;
    private readonly IRepository<AppProduct, Guid> _productRepo;
    private readonly IRepository<AppBranch, Guid> _branchRepo;
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepo;
    private readonly IRepository<AppStockBatch, Guid> _batchRepo;
    private readonly IProductionFormulaRepository _formulaRepo;
    private readonly IBranchProductionRequestRepository _requestRepo;
    private readonly IProductionPlanRepository _planRepo;
    private readonly IProductionOrderRepository _orderRepo;
    private readonly IdentityUserManager _userManager;
    private readonly ILogger<ProductionDemoSeedContributor> _logger;

    public ProductionDemoSeedContributor(
        CategoryManager categoryManager,
        SupplierManager supplierManager,
        ProductManager productManager,
        BranchInventoryManager inventoryManager,
        StockBatchManager stockBatchManager,
        ProductionFormulaManager formulaManager,
        BranchProductionRequestManager requestManager,
        ProductionPlanManager planManager,
        ProductionOrderManager orderManager,
        IRepository<AppCategory, Guid> categoryRepo,
        IRepository<AppSupplier, Guid> supplierRepo,
        IRepository<AppProduct, Guid> productRepo,
        IRepository<AppBranch, Guid> branchRepo,
        IRepository<AppBranchInventory, Guid> inventoryRepo,
        IRepository<AppStockBatch, Guid> batchRepo,
        IProductionFormulaRepository formulaRepo,
        IBranchProductionRequestRepository requestRepo,
        IProductionPlanRepository planRepo,
        IProductionOrderRepository orderRepo,
        IdentityUserManager userManager,
        ILogger<ProductionDemoSeedContributor> logger)
    {
        _categoryManager = categoryManager;
        _supplierManager = supplierManager;
        _productManager = productManager;
        _inventoryManager = inventoryManager;
        _stockBatchManager = stockBatchManager;
        _formulaManager = formulaManager;
        _requestManager = requestManager;
        _planManager = planManager;
        _orderManager = orderManager;
        _categoryRepo = categoryRepo;
        _supplierRepo = supplierRepo;
        _productRepo = productRepo;
        _branchRepo = branchRepo;
        _inventoryRepo = inventoryRepo;
        _batchRepo = batchRepo;
        _formulaRepo = formulaRepo;
        _requestRepo = requestRepo;
        _planRepo = planRepo;
        _orderRepo = orderRepo;
        _userManager = userManager;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        var mainKitchen = await _branchRepo.FindAsync(b => b.Name == IdentityDataSeedContributor.MainKitchenBranchName);
        if (mainKitchen == null)
        {
            _logger.LogWarning("Production Arabic demo seed skipped because the Main Kitchen branch does not exist yet.");
            return;
        }

        var rawCategory = await EnsureCategoryAsync(
            RawCategoryName,
            "مواد تدخل في وصفات المطبخ الرئيسي مثل الدقيق والزبدة والسكر والبيض.");
        var packagingCategory = await EnsureCategoryAsync(
            PackagingCategoryName,
            "علب وأكياس تستخدم لتجهيز الإنتاج قبل إرساله للفروع.");
        var supplier = await EnsureSupplierAsync();

        var ingredients = await EnsureIngredientProductsAsync(rawCategory.Id, packagingCategory.Id, supplier.Id);
        await EnsureKitchenIngredientStockAsync(mainKitchen.Id, ingredients);

        var finishedProducts = await EnsureFinishedProductsAreProducibleAsync();
        await EnsureArabicFormulasAsync(finishedProducts, ingredients);

        var requests = await EnsureBranchRequestsAsync(finishedProducts);
        await EnsureStarterPlanAndOrdersAsync(mainKitchen.Id, requests);
        await EnsureButterShortageScenarioAsync(mainKitchen.Id, finishedProducts);
    }

    private async Task<AppCategory> EnsureCategoryAsync(string name, string description)
    {
        var category = await _categoryRepo.FindAsync(c => c.Name == name);
        if (category != null)
        {
            return category;
        }

        category = await _categoryManager.CreateAsync(name, description);
        await _categoryRepo.InsertAsync(category, autoSave: true);
        return category;
    }

    private async Task<AppSupplier> EnsureSupplierAsync()
    {
        var supplier = await _supplierRepo.FindAsync(s => s.Name == DemoSupplierName);
        if (supplier != null)
        {
            return supplier;
        }

        supplier = await _supplierManager.CreateAsync(
            DemoSupplierName,
            contactPerson: "قسم المشتريات",
            phone: "+963-11-555-0101",
            email: "production.demo@example.com",
            address: "توريد تجريبي للمطبخ الرئيسي",
            leadTimeDays: 2);
        await _supplierRepo.InsertAsync(supplier, autoSave: true);
        return supplier;
    }

    private async Task<Dictionary<string, AppProduct>> EnsureIngredientProductsAsync(
        Guid rawCategoryId,
        Guid packagingCategoryId,
        Guid supplierId)
    {
        var specs = new[]
        {
            new IngredientSpec("RM-FLOUR-001", "دقيق فاخر للكرواسون", "غرام", rawCategoryId, 0.002m, 180, ProductTypes.RawMaterial, "دقيق أبيض عالي البروتين مناسب للعجين المورق والخبز."),
            new IngredientSpec("RM-BUTTER-001", "زبدة فرنسية غير مملحة", "غرام", rawCategoryId, 0.012m, 45, ProductTypes.RawMaterial, "زبدة باردة للتوريق والكرواسون."),
            new IngredientSpec("RM-SUGAR-001", "سكر ناعم", "غرام", rawCategoryId, 0.0015m, 365, ProductTypes.RawMaterial, "سكر للحشوات والكريمات والعجين."),
            new IngredientSpec("RM-EGG-001", "بيض طازج", "حبة", rawCategoryId, 0.18m, 14, ProductTypes.RawMaterial, "بيض يومي للكيك والتلميع والكريم."),
            new IngredientSpec("RM-CHOC-001", "شوكولا داكنة 70%", "غرام", rawCategoryId, 0.018m, 180, ProductTypes.RawMaterial, "شوكولا للحشوات وكيك الأوبرا."),
            new IngredientSpec("RM-ALMOND-001", "لوز مطحون", "غرام", rawCategoryId, 0.02m, 120, ProductTypes.RawMaterial, "بودرة لوز للحلويات الفرنسية."),
            new IngredientSpec("RM-STRAW-001", "فراولة طازجة", "غرام", rawCategoryId, 0.01m, 4, ProductTypes.RawMaterial, "فراولة للتارت والطلبات الطازجة."),
            new IngredientSpec("RM-YEAST-001", "خميرة فورية", "غرام", rawCategoryId, 0.006m, 180, ProductTypes.RawMaterial, "خميرة للخبز والعجين اليومي."),
            new IngredientSpec("RM-SALT-001", "ملح غذائي", "غرام", rawCategoryId, 0.001m, 730, ProductTypes.RawMaterial, "ملح لضبط نكهة العجين."),
            new IngredientSpec("PK-BOX-001", "علبة كرتون للحلويات", "علبة", packagingCategoryId, 0.25m, null, ProductTypes.Packaging, "علبة تغليف للطلبات الجاهزة والتحويلات.")
        };

        var products = new Dictionary<string, AppProduct>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in specs)
        {
            var product = await _productRepo.FindAsync(p => p.SKU == spec.Sku);
            if (product == null)
            {
                product = await _productManager.CreateAsync(
                    spec.CategoryId,
                    spec.Name,
                    spec.Sku,
                    spec.Unit,
                    supplierId,
                    spec.Description,
                    spec.CostPrice,
                    salePrice: 0m,
                    reorderLevel: spec.ProductType == ProductTypes.Packaging ? 50 : 1_000,
                    shelfLifeDays: spec.ShelfLifeDays,
                    productType: spec.ProductType,
                    isSellable: false,
                    isPurchasable: true,
                    isProducible: false);

                await _productRepo.InsertAsync(product, autoSave: true);
            }

            products[spec.Sku] = product;
        }

        return products;
    }

    private async Task EnsureKitchenIngredientStockAsync(
        Guid mainKitchenId,
        Dictionary<string, AppProduct> ingredients)
    {
        var quantities = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["RM-FLOUR-001"] = 40_000,
            ["RM-BUTTER-001"] = 18_000,
            ["RM-SUGAR-001"] = 15_000,
            ["RM-EGG-001"] = 360,
            ["RM-CHOC-001"] = 12_000,
            ["RM-ALMOND-001"] = 8_000,
            ["RM-STRAW-001"] = 6_000,
            ["RM-YEAST-001"] = 2_000,
            ["RM-SALT-001"] = 3_000,
            ["PK-BOX-001"] = 300
        };

        foreach (var (sku, quantity) in quantities)
        {
            var product = ingredients[sku];
            var exists = await _inventoryRepo.AnyAsync(i => i.BranchId == mainKitchenId && i.ProductId == product.Id);
            if (!exists)
            {
                var inventory = await _inventoryManager.InitializeAsync(
                    mainKitchenId,
                    product.Id,
                    quantity,
                    minimumStock: Math.Max(5, quantity / 10),
                    maximumStock: quantity * 2);
                await _inventoryRepo.InsertAsync(inventory, autoSave: true);
            }

            if (product.ShelfLifeDays.HasValue
                && !await _batchRepo.AnyAsync(b =>
                    b.BranchId == mainKitchenId
                    && b.ProductId == product.Id
                    && b.SourceType == StockBatchSourceTypes.Seed))
            {
                await _stockBatchManager.CreateAsync(
                    mainKitchenId,
                    product.Id,
                    quantity,
                    DateTime.UtcNow.Date.AddDays(product.ShelfLifeDays.Value),
                    StockBatchSourceTypes.Seed,
                    autoSave: true);
            }
        }
    }

    private async Task<Dictionary<string, AppProduct>> EnsureFinishedProductsAreProducibleAsync()
    {
        var targetSkus = new[] { "VN-001", "VN-002", "CT-001", "CT-002", "BR-001" };
        var products = await _productRepo.GetListAsync(p => targetSkus.Contains(p.SKU));
        var bySku = products.ToDictionary(p => p.SKU, StringComparer.OrdinalIgnoreCase);

        foreach (var sku in targetSkus)
        {
            if (!bySku.TryGetValue(sku, out var product))
            {
                _logger.LogWarning("Production Arabic demo seed could not find finished product SKU {Sku}; related formula/request will be skipped.", sku);
                continue;
            }

            if (!product.IsProducible || product.ProductType != ProductTypes.FinishedGood)
            {
                product.SetClassification(ProductTypes.FinishedGood, isSellable: true, isPurchasable: true, isProducible: true);
                await _productRepo.UpdateAsync(product, autoSave: true);
            }
        }

        return bySku;
    }

    private async Task EnsureArabicFormulasAsync(
        Dictionary<string, AppProduct> finishedProducts,
        Dictionary<string, AppProduct> ingredients)
    {
        var formulaSpecs = new[]
        {
            new FormulaSpec(
                "VN-001",
                "وصفة كرواسون الزبدة - دفعة 50 قطعة",
                50,
                4m,
                18m,
                7m,
                180,
                "وصفة تشغيل واضحة: عجين مورق، راحة باردة، ثم خبز وتبريد قبل التحويل للفروع.",
                new[]
                {
                    Item("RM-FLOUR-001", 5_000, 2m),
                    Item("RM-BUTTER-001", 2_500, 3m),
                    Item("RM-SUGAR-001", 350, 1m),
                    Item("RM-EGG-001", 20, 0m),
                    Item("RM-YEAST-001", 120, 0m),
                    Item("RM-SALT-001", 80, 0m)
                }),
            new FormulaSpec(
                "VN-002",
                "وصفة بان أو شوكولا - دفعة 40 قطعة",
                40,
                4.5m,
                20m,
                8m,
                190,
                "دفعة كرواسون بالشوكولا مع مراقبة كمية الحشوة حتى لا يزيد الهدر.",
                new[]
                {
                    Item("RM-FLOUR-001", 4_200, 2m),
                    Item("RM-BUTTER-001", 2_200, 3m),
                    Item("RM-SUGAR-001", 300, 1m),
                    Item("RM-EGG-001", 16, 0m),
                    Item("RM-CHOC-001", 1_200, 1m),
                    Item("RM-YEAST-001", 100, 0m),
                    Item("RM-SALT-001", 70, 0m)
                }),
            new FormulaSpec(
                "CT-001",
                "وصفة تارت الفراولة - دفعة 12 قطعة",
                12,
                6m,
                14m,
                5m,
                120,
                "تارت سريع التلف، لذلك تظهر الفراولة الطازجة كعنصر واضح في الوصفة.",
                new[]
                {
                    Item("RM-FLOUR-001", 1_800, 1m),
                    Item("RM-BUTTER-001", 900, 2m),
                    Item("RM-SUGAR-001", 700, 1m),
                    Item("RM-EGG-001", 24, 0m),
                    Item("RM-STRAW-001", 1_800, 4m),
                    Item("PK-BOX-001", 12, 0m)
                }),
            new FormulaSpec(
                "CT-002",
                "وصفة كيك أوبرا - دفعة 16 شريحة",
                16,
                5m,
                22m,
                9m,
                160,
                "دفعة حلويات دقيقة تحتاج شوكولا ولوز، مناسبة لمراجعة تكلفة الوصفة.",
                new[]
                {
                    Item("RM-FLOUR-001", 1_500, 1m),
                    Item("RM-BUTTER-001", 900, 2m),
                    Item("RM-SUGAR-001", 800, 1m),
                    Item("RM-EGG-001", 30, 0m),
                    Item("RM-CHOC-001", 2_000, 2m),
                    Item("RM-ALMOND-001", 700, 1m)
                }),
            new FormulaSpec(
                "BR-001",
                "وصفة باغيت تقليدي - دفعة 80 رغيف",
                80,
                3m,
                12m,
                4m,
                150,
                "دفعة خبز يومية تعتمد على دقيق وخميرة وملح، سهلة القراءة للمطبخ.",
                new[]
                {
                    Item("RM-FLOUR-001", 9_000, 1m),
                    Item("RM-YEAST-001", 220, 0m),
                    Item("RM-SALT-001", 160, 0m),
                    Item("RM-SUGAR-001", 200, 0m)
                })
        };

        foreach (var spec in formulaSpecs)
        {
            if (!finishedProducts.TryGetValue(spec.FinishedSku, out var finishedProduct))
            {
                continue;
            }

            var hasDefaultFormula = await _formulaRepo.AnyAsync(f =>
                f.FinishedProductId == finishedProduct.Id
                && f.IsActive
                && f.IsDefault);
            if (hasDefaultFormula)
            {
                continue;
            }

            await _formulaManager.EnsureIngredientsAreValidAsync(
                spec.Items.Select(i => ingredients[i.IngredientSku].Id));

            var formula = await _formulaManager.CreateAsync(
                finishedProduct.Id,
                spec.Name,
                spec.OutputQuantity,
                version: 1,
                spec.ExpectedWastePercent,
                spec.LaborCostPerBatch,
                spec.OverheadCostPerBatch,
                spec.EstimatedProductionMinutes,
                isActive: true,
                isDefault: true,
                notes: $"{spec.Notes} {DemoMarker}");

            var sortOrder = 1;
            foreach (var item in spec.Items)
            {
                formula.AddItem(
                    Guid.NewGuid(),
                    ingredients[item.IngredientSku].Id,
                    item.Quantity,
                    item.LossPercent,
                    sortOrder++);
            }

            await _formulaRepo.InsertAsync(formula, autoSave: true);
        }
    }

    private async Task<List<AppBranchProductionRequest>> EnsureBranchRequestsAsync(
        Dictionary<string, AppProduct> finishedProducts)
    {
        var existing = await _requestRepo.GetListAsync(r => r.Notes != null && r.Notes.Contains(DemoMarker));
        if (existing.Count > 0)
        {
            return existing;
        }

        var managerUser = await _userManager.FindByNameAsync(IdentityDataSeedContributor.BranchManagerUserName);
        var requests = new List<AppBranchProductionRequest>();
        var specs = new[]
        {
            new BranchRequestSpec(
                "بوتيك الشارع الرئيسي",
                ProductionPriorities.Urgent,
                "طلب واضح للمطبخ: الفرع يريد أصناف الفطور والحلويات قبل ازدحام الصباح.",
                new Dictionary<string, int>
                {
                    ["VN-001"] = 35,
                    ["VN-002"] = 25,
                    ["CT-001"] = 8
                }),
            new BranchRequestSpec(
                "مقهى ضفة النهر",
                ProductionPriorities.Normal,
                "طلب تجريبي للفرع: خبز يومي مع كمية حلويات تكفي واجهة العرض.",
                new Dictionary<string, int>
                {
                    ["BR-001"] = 45,
                    ["CT-002"] = 12,
                    ["VN-001"] = 18
                })
        };

        foreach (var spec in specs)
        {
            var branch = await _branchRepo.FindAsync(b => b.Name == spec.BranchName);
            if (branch == null)
            {
                _logger.LogWarning("Production Arabic demo seed could not find branch {BranchName}; request skipped.", spec.BranchName);
                continue;
            }

            var productIds = spec.Quantities
                .Where(x => finishedProducts.ContainsKey(x.Key))
                .Select(x => finishedProducts[x.Key].Id)
                .ToList();
            await _requestManager.EnsureRequestProductsAreProducibleAsync(productIds);

            var request = await _requestManager.CreateAsync(
                branch.Id,
                DateTime.UtcNow.Date.AddDays(1),
                spec.Priority,
                managerUser?.Id,
                $"{spec.Notes} {DemoMarker}");

            foreach (var (sku, quantity) in spec.Quantities)
            {
                if (!finishedProducts.TryGetValue(sku, out var product))
                {
                    continue;
                }

                request.AddItem(
                    Guid.NewGuid(),
                    product.Id,
                    quantity,
                    "كمية مقترحة حتى تظهر خطة الإنتاج بصورة مفهومة.");
            }

            request.Submit();
            request.Approve(
                managerUser?.Id,
                request.Items.ToDictionary(i => i.Id, i => i.RequestedQuantity),
                "اعتماد تجريبي: الكميات مناسبة لخطة إنتاج الغد.");

            await _requestRepo.InsertAsync(request, autoSave: true);
            requests.Add(request);
        }

        return requests;
    }

    private async Task EnsureStarterPlanAndOrdersAsync(
        Guid mainKitchenId,
        List<AppBranchProductionRequest> requests)
    {
        if (requests.Count == 0)
        {
            return;
        }

        var existingPlan = await _planRepo.AnyAsync(p => p.Notes != null && p.Notes.Contains(DemoMarker));
        if (existingPlan)
        {
            return;
        }

        var kitchenUser = await _userManager.FindByNameAsync(IdentityDataSeedContributor.KitchenManagerUserName);
        var plan = await _planManager.CreateDraftAsync(
            mainKitchenId,
            DateTime.UtcNow.Date.AddDays(1),
            kitchenUser?.Id,
            $"خطة تجريبية عربية: تجمع طلبات الفروع المعتمدة وتحوّلها إلى أوامر طبخ جاهزة. {DemoMarker}");

        if (plan.Lines.Count == 0)
        {
            _logger.LogWarning("Production Arabic demo seed created no plan lines; approved demo requests may not match producible products.");
            return;
        }

        foreach (var line in plan.Lines.ToList())
        {
            var hasDefaultFormula = await _formulaRepo.AnyAsync(f =>
                f.FinishedProductId == line.ProductId
                && f.IsActive
                && f.IsDefault);
            if (!hasDefaultFormula)
            {
                plan.UpdateLinePlannedQuantity(
                    line.Id,
                    0,
                    "تم استبعاد هذا الصنف من خطة البذرة لأنه لا يملك وصفة إنتاج افتراضية بعد.");
            }
        }

        plan.Confirm(kitchenUser?.Id);
        var orders = await _orderManager.CreateFromPlanAsync(plan, kitchenUser?.Id);

        await _planRepo.InsertAsync(plan);
        foreach (var order in orders)
        {
            await _orderRepo.InsertAsync(order);
        }

        await _planRepo.UpdateAsync(plan, autoSave: true);
    }

    private async Task EnsureButterShortageScenarioAsync(
        Guid mainKitchenId,
        Dictionary<string, AppProduct> finishedProducts)
    {
        if (await _orderRepo.AnyAsync(o => o.Notes != null && o.Notes.Contains(ShortageDemoMarker)))
        {
            return;
        }

        if (!finishedProducts.TryGetValue(ShortageDemoFinishedSku, out var finishedProduct))
        {
            _logger.LogWarning(
                "Production Arabic shortage demo seed could not find finished product SKU {Sku}; shortage scenario skipped.",
                ShortageDemoFinishedSku);
            return;
        }

        var branch = await _branchRepo.FindAsync(b => b.Name == "مقهى ضفة النهر");
        if (branch == null)
        {
            _logger.LogWarning("Production Arabic shortage demo seed could not find Riverside branch; shortage scenario skipped.");
            return;
        }

        var kitchenUser = await _userManager.FindByNameAsync(IdentityDataSeedContributor.KitchenManagerUserName);
        var branchUser = await _userManager.FindByNameAsync(IdentityDataSeedContributor.BranchManagerUserName);
        var request = (await _requestRepo.GetListAsync(r => r.Notes != null && r.Notes.Contains(ShortageDemoMarker)))
            .FirstOrDefault();

        if (request == null)
        {
            await _requestManager.EnsureRequestProductsAreProducibleAsync(new[] { finishedProduct.Id });

            request = await _requestManager.CreateAsync(
                branch.Id,
                DateTime.UtcNow.Date.AddDays(1),
                ProductionPriorities.Urgent,
                branchUser?.Id,
                $"طلب تجريبي مقصود: فرع ضفة النهر يحتاج بان أو شوكولا بكمية كبيرة قبل الذروة. هذا الطلب صُمم ليظهر نقص الزبدة فقط بوضوح على شاشة الطبخ. {ShortageDemoMarker}");

            request.AddItem(
                Guid.NewGuid(),
                finishedProduct.Id,
                ShortageDemoQuantity,
                "كمية مقصودة للعرض: كل الخامات تكفي تقريبًا ما عدا الزبدة.");

            request.Submit();
            request.Approve(
                kitchenUser?.Id,
                request.Items.ToDictionary(i => i.Id, i => i.RequestedQuantity),
                "اعتماد تجريبي حتى تظهر رحلة النقص ثم الشراء ثم الطبخ ثم الصرف.");

            await _requestRepo.InsertAsync(request, autoSave: true);
        }

        var order = await _orderManager.CreateAsync(
            mainKitchenId,
            productionPlanId: null,
            productionPlanLineId: null,
            finishedProduct.Id,
            ShortageDemoQuantity,
            ProductionPriorities.Urgent,
            kitchenUser?.Id,
            $"أمر طبخ تجريبي مقصود لمسار العرض الكامل: نقص زبدة -> طلب شراء خامات -> استلام -> طبخ -> صرف للفرع. {ShortageDemoMarker}");

        if (order.Status != ProductionOrderStatuses.WaitingForIngredients)
        {
            _logger.LogWarning(
                "Production Arabic shortage demo expected order {OrderNumber} to wait for ingredients, but it is {Status}. The database may already contain extra butter stock.",
                order.OrderNumber,
                order.Status);
        }

        await _orderRepo.InsertAsync(order, autoSave: true);
    }

    private static FormulaItemSpec Item(string ingredientSku, int quantity, decimal lossPercent) =>
        new(ingredientSku, quantity, lossPercent);

    private sealed record IngredientSpec(
        string Sku,
        string Name,
        string Unit,
        Guid CategoryId,
        decimal CostPrice,
        int? ShelfLifeDays,
        string ProductType,
        string Description);

    private sealed record FormulaItemSpec(string IngredientSku, int Quantity, decimal LossPercent);

    private sealed record FormulaSpec(
        string FinishedSku,
        string Name,
        int OutputQuantity,
        decimal ExpectedWastePercent,
        decimal LaborCostPerBatch,
        decimal OverheadCostPerBatch,
        int EstimatedProductionMinutes,
        string Notes,
        IReadOnlyList<FormulaItemSpec> Items);

    private sealed record BranchRequestSpec(
        string BranchName,
        string Priority,
        string Notes,
        IReadOnlyDictionary<string, int> Quantities);
}
