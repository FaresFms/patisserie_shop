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
using Production.Waste;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace patisserie_shop.DbMigrator;

public class ProductionDemoSeedContributor : IDataSeedContributor, ITransientDependency
{
    private const string ShortageFinishedSku = "VN-002";
    private const int ShortageQuantity = 340;
    private const string RawCategoryName = "مواد الإنتاج الخام";
    private const string PackagingCategoryName = "مواد التغليف";
    private const string IngredientSupplierName = "شركة زاد الشام للمواد الغذائية";

    private readonly CategoryManager _categoryManager;
    private readonly SupplierManager _supplierManager;
    private readonly ProductManager _productManager;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly StockBatchManager _stockBatchManager;
    private readonly ProductionFormulaManager _formulaManager;
    private readonly BranchProductionRequestManager _requestManager;
    private readonly ProductionPlanManager _planManager;
    private readonly ProductionOrderManager _orderManager;
    private readonly ProductionWasteManager _wasteManager;

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
    private readonly IProductionWasteRepository _wasteRepo;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
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
        ProductionWasteManager wasteManager,
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
        IProductionWasteRepository wasteRepo,
        IdentityUserManager userManager,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
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
        _wasteManager = wasteManager;
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
        _wasteRepo = wasteRepo;
        _userManager = userManager;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        var kitchenUser = await _userManager.FindByNameAsync(
            IdentityDataSeedContributor.KitchenManagerUserName);
        if (kitchenUser == null)
        {
            _logger.LogWarning(
                "Production presentation data could not be created because the kitchen manager does not exist.");
            return;
        }

        var mainKitchen = await _branchRepo.FindAsync(
            b => b.BranchType == BranchTypes.MainKitchen
                 && b.ManagerUserId == kitchenUser.Id);
        if (mainKitchen == null)
        {
            _logger.LogWarning(
                "Production presentation data could not be created because the central kitchen does not exist.");
            return;
        }

        using var actorScope = _currentPrincipalAccessor.Change(
            GraduationSeedData.PrincipalFor(
                kitchenUser,
                IdentityDataSeedContributor.KitchenManagerRoleName));

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
        await EnsureCompletedProductionAndWasteAsync(
            mainKitchen.Id,
            kitchenUser.Id,
            finishedProducts,
            ingredients);
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
        var supplier = await _supplierRepo.FindAsync(s => s.Name == IngredientSupplierName);
        if (supplier != null)
        {
            return supplier;
        }

        supplier = await _supplierManager.CreateAsync(
            IngredientSupplierName,
            contactPerson: "سليم العطار",
            phone: "+963-11-555-0101",
            email: "orders@zad-alsham.example",
            address: "سوق الهال، دمشق",
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
                    && b.SourceType == StockBatchSourceTypes.Adjustment))
            {
                await _stockBatchManager.CreateAsync(
                    mainKitchenId,
                    product.Id,
                    quantity,
                    DateTime.UtcNow.Date.AddDays(product.ShelfLifeDays.Value),
                    StockBatchSourceTypes.Adjustment,
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
                _logger.LogWarning("Production data could not find finished product SKU {Sku}; its formula and requests will be skipped.", sku);
                continue;
            }

            if (!product.IsProducible || product.ProductType != ProductTypes.FinishedGood)
            {
                product.SetClassification(ProductTypes.FinishedGood, isSellable: true, isPurchasable: false, isProducible: true);
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
                notes: spec.Notes);

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
        var existing = await _requestRepo.GetListAsync();
        if (existing.Count > 0)
        {
            return existing;
        }

        var requests = new List<AppBranchProductionRequest>();
        var specs = new[]
        {
            new BranchRequestSpec(
                "فرع المزة",
                ProductionPriorities.Urgent,
                "نحتاج أصناف الفطور قبل الساعة السابعة بسبب حجوزات الشركات الصباحية.",
                new Dictionary<string, int>
                {
                    ["VN-001"] = 35,
                    ["VN-002"] = 25,
                    ["CT-001"] = 8
                }),
            new BranchRequestSpec(
                "فرع المالكي",
                ProductionPriorities.Normal,
                "تعبئة واجهة الحلويات والخبز اليومي قبل بدء دوام المساء.",
                new Dictionary<string, int>
                {
                    ["BR-001"] = 45,
                    ["CT-002"] = 12,
                    ["VN-001"] = 18
                }),
            new BranchRequestSpec(
                "فرع أبو رمانة",
                ProductionPriorities.Normal,
                "طلب نهاية الأسبوع مع تركيز على التارت وشرائح الكيك.",
                new Dictionary<string, int>
                {
                    ["CT-001"] = 18,
                    ["CT-002"] = 20,
                    ["VN-002"] = 24
                }),
            new BranchRequestSpec(
                "فرع الشعلان",
                ProductionPriorities.Urgent,
                "الكمية المتوفرة لا تكفي حركة يوم الجمعة؛ الأولوية للكرواسون والباغيت.",
                new Dictionary<string, int>
                {
                    ["VN-001"] = 42,
                    ["VN-002"] = 32,
                    ["BR-001"] = 55
                }),
            new BranchRequestSpec(
                "فرع باب توما",
                ProductionPriorities.Normal,
                "تجهيز دفعة متوازنة لفترة الازدحام المسائية.",
                new Dictionary<string, int>
                {
                    ["VN-001"] = 22,
                    ["BR-001"] = 35,
                    ["CT-001"] = 10
                })
        };

        foreach (var spec in specs)
        {
            var branch = await _branchRepo.FindAsync(b => b.Name == spec.BranchName);
            if (branch == null)
            {
                _logger.LogWarning("Production request branch {BranchName} was not found.", spec.BranchName);
                continue;
            }

            var branchSpec = GraduationSeedData.RetailBranches.First(x => x.Name == spec.BranchName);
            var managerUser = await _userManager.FindByNameAsync(branchSpec.ManagerUserName);
            using var requestActor = managerUser == null
                ? null
                : _currentPrincipalAccessor.Change(
                    GraduationSeedData.PrincipalFor(
                        managerUser,
                        IdentityDataSeedContributor.BranchManagerRoleName));
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
                spec.Notes);

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
                    "الكمية مبنية على مبيعات آخر أسبوع والمخزون المتاح بالفرع.");
            }

            request.Submit();
            request.Approve(
                managerUser?.Id,
                request.Items.ToDictionary(i => i.Id, i => i.RequestedQuantity),
                "تمت مراجعة الكميات واعتمادها ضمن طاقة المطبخ ليوم الغد.");

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

        var existingPlan = await _planRepo.AnyAsync(p =>
            p.KitchenBranchId == mainKitchenId
            && p.ProductionDate == DateTime.UtcNow.Date.AddDays(1));
        if (existingPlan)
        {
            return;
        }

        var kitchenUser = await _userManager.FindByNameAsync(IdentityDataSeedContributor.KitchenManagerUserName);
        var plan = await _planManager.CreateDraftAsync(
            mainKitchenId,
            DateTime.UtcNow.Date.AddDays(1),
            kitchenUser?.Id,
            "خطة إنتاج الغد مجمّعة من طلبات الفروع المعتمدة، مع ترتيب الخبز والمعجنات قبل الحلويات.");

        if (plan.Lines.Count == 0)
        {
            _logger.LogWarning("The production plan has no lines because its approved requests do not match producible products.");
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
                    "انشال هالصنف من الخطة لأنه لسه ما عنده وصفة إنتاج معتمدة.");
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
        if (!finishedProducts.TryGetValue(ShortageFinishedSku, out var finishedProduct))
        {
            _logger.LogWarning(
                "The shortage scenario could not find finished product SKU {Sku}.",
                ShortageFinishedSku);
            return;
        }

        if (await _orderRepo.AnyAsync(o =>
                o.ProductionPlanId == null
                && o.FinishedProductId == finishedProduct.Id
                && o.PlannedOutputQuantity == ShortageQuantity))
        {
            return;
        }

        var branch = await _branchRepo.FindAsync(b => b.Name == "فرع جرمانا");
        if (branch == null)
        {
            _logger.LogWarning("The shortage scenario could not find the Jaramana branch.");
            return;
        }

        var kitchenUser = await _userManager.FindByNameAsync(IdentityDataSeedContributor.KitchenManagerUserName);
        var branchUser = await _userManager.FindByNameAsync("manager8");
        const string requestNote = "طلب تجهيز مبكر لفعالية كبيرة في فرع جرمانا، والكمية أعلى من المعتاد بسبب الحجز المسبق.";
        var request = (await _requestRepo.GetListAsync(r => r.Notes == requestNote))
            .FirstOrDefault();

        if (request == null)
        {
            await _requestManager.EnsureRequestProductsAreProducibleAsync(new[] { finishedProduct.Id });

            request = await _requestManager.CreateAsync(
                branch.Id,
                DateTime.UtcNow.Date.AddDays(1),
                ProductionPriorities.Urgent,
                branchUser?.Id,
                requestNote);

            request.AddItem(
                Guid.NewGuid(),
                finishedProduct.Id,
                ShortageQuantity,
                "حجز مسبق لفعالية مسائية؛ لازم تكون الدفعة جاهزة قبل موعد سيارة التوزيع.");

            request.Submit();
            request.Approve(
                kitchenUser?.Id,
                request.Items.ToDictionary(i => i.Id, i => i.RequestedQuantity),
                "اعتمدنا الطلب، لكن لازم قسم المشتريات يؤمّن كمية الزبدة الناقصة قبل بدء الطبخ.");

            await _requestRepo.InsertAsync(request, autoSave: true);
        }

        var order = await _orderManager.CreateAsync(
            mainKitchenId,
            productionPlanId: null,
            productionPlanLineId: null,
            finishedProduct.Id,
            ShortageQuantity,
            ProductionPriorities.Urgent,
            kitchenUser?.Id,
            "أمر عاجل لحجز جرمانا. الحالة بانتظار تأمين الزبدة من المورد ثم يُستكمل الطبخ والتوزيع.");

        if (order.Status != ProductionOrderStatuses.WaitingForIngredients)
        {
            _logger.LogWarning(
                "The large reservation order {OrderNumber} was expected to wait for ingredients but its status is {Status}.",
                order.OrderNumber,
                order.Status);
        }

        await _orderRepo.InsertAsync(order, autoSave: true);
    }

    private async Task EnsureCompletedProductionAndWasteAsync(
        Guid mainKitchenId,
        Guid kitchenUserId,
        Dictionary<string, AppProduct> finishedProducts,
        Dictionary<string, AppProduct> ingredients)
    {
        const string completedOrderNote =
            "دفعة الصباح اكتملت بعد فحص اللون والقوام، وتم عزل القطع المتضررة قبل التغليف.";

        if (!await _orderRepo.AnyAsync(o => o.Notes == completedOrderNote)
            && finishedProducts.TryGetValue("VN-001", out var croissant))
        {
            var order = await _orderManager.CreateAsync(
                mainKitchenId,
                productionPlanId: null,
                productionPlanLineId: null,
                croissant.Id,
                plannedOutputQuantity: 50,
                ProductionPriorities.Normal,
                kitchenUserId,
                completedOrderNote,
                sequence: 90);

            if (order.Status == ProductionOrderStatuses.ReadyToCook)
            {
                var consumption = order.Start(kitchenUserId);
                foreach (var line in consumption)
                {
                    var inventory = await _inventoryRepo.FindAsync(i =>
                        i.BranchId == mainKitchenId
                        && i.ProductId == line.IngredientProductId);
                    if (inventory == null)
                    {
                        throw new InvalidOperationException(
                            $"Missing kitchen inventory for production ingredient '{line.IngredientProductId}'.");
                    }

                    await _inventoryManager.AdjustStockAsync(
                        inventory,
                        inventory.QuantityOnHand - line.Quantity,
                        StockMovementTypes.ProductionConsumption,
                        "استهلاك خامات دفعة كرواسون الصباح.",
                        order.Id,
                        "ProductionOrder");
                    await _inventoryRepo.UpdateAsync(inventory);
                }

                var completion = order.Complete(
                    actualOutputQuantity: 50,
                    acceptedQuantity: 47,
                    rejectedQuantity: 3,
                    expiryDate: DateTime.UtcNow.Date.AddDays(croissant.ShelfLifeDays ?? 2),
                    ProductionWasteReasons.ShapeDamaged,
                    kitchenUserId,
                    completedOrderNote);
                await _orderManager.ApplyAllocationReleasesAsync(completion.ReleasedAllocations);

                var finishedInventory = await _inventoryRepo.FindAsync(i =>
                    i.BranchId == mainKitchenId
                    && i.ProductId == croissant.Id);
                if (finishedInventory == null)
                {
                    finishedInventory = await _inventoryManager.InitializeAsync(
                        mainKitchenId,
                        croissant.Id);
                    await _inventoryRepo.InsertAsync(finishedInventory);
                }

                await _inventoryManager.AdjustStockAsync(
                    finishedInventory,
                    finishedInventory.QuantityOnHand + completion.Output.AcceptedQuantity,
                    StockMovementTypes.ProductionOutput,
                    "إنتاج مقبول بعد فحص الجودة.",
                    order.Id,
                    "ProductionOrder",
                    batchExpiryDate: completion.Output.ExpiryDate);
                await _inventoryRepo.UpdateAsync(finishedInventory);

                var rejectedWaste = await _wasteManager.CreateAsync(
                    order.Id,
                    mainKitchenId,
                    croissant.Id,
                    ProductionWasteTypes.RejectedOutput,
                    quantity: 3,
                    unitCost: order.UnitProductionCost,
                    ProductionWasteReasons.ShapeDamaged,
                    kitchenUserId,
                    DateTime.UtcNow.AddHours(-3),
                    "ثلاث قطع تشوّه شكلها أثناء النقل من صينية الخَبز، لذلك لم تخرج للفروع.");
                await _wasteRepo.InsertAsync(rejectedWaste);
            }

            await _orderRepo.InsertAsync(order, autoSave: true);
        }

        if (await _wasteRepo.AnyAsync(w => w.Notes == "تلفت الفراولة بسبب ارتفاع حرارة البراد خلال الصيانة."))
        {
            return;
        }

        await RecordStockWasteAsync(
            mainKitchenId,
            ingredients["RM-STRAW-001"],
            quantity: 450,
            ProductionWasteTypes.IngredientSpoilage,
            ProductionWasteReasons.IngredientSpoilage,
            kitchenUserId,
            DateTime.UtcNow.AddDays(-12),
            "تلفت الفراولة بسبب ارتفاع حرارة البراد خلال الصيانة.");

        await RecordStockWasteAsync(
            mainKitchenId,
            ingredients["PK-BOX-001"],
            quantity: 12,
            ProductionWasteTypes.ManualWriteOff,
            ProductionWasteReasons.PackagingDamage,
            kitchenUserId,
            DateTime.UtcNow.AddDays(-8),
            "وصلت عدة علب مضغوطة من الزاوية وما عادت مناسبة لتغليف طلبات الزبائن.");

        if (finishedProducts.TryGetValue("BR-001", out var baguette))
        {
            await RecordStockWasteAsync(
                mainKitchenId,
                baguette,
                quantity: 6,
                ProductionWasteTypes.ExpiredFinishedGood,
                ProductionWasteReasons.ExpiredBeforeDispatch,
                kitchenUserId,
                DateTime.UtcNow.AddDays(-5),
                "ستة أرغفة بقيت بعد إلغاء رحلة التوزيع المسائية وانتهت صلاحيتها قبل الإرسال.");
        }

        if (finishedProducts.TryGetValue("CT-002", out var operaCake))
        {
            var qualityWaste = await _wasteManager.CreateAsync(
                productionOrderId: null,
                mainKitchenId,
                operaCake.Id,
                ProductionWasteTypes.RejectedOutput,
                quantity: 4,
                operaCake.CostPrice,
                ProductionWasteReasons.UnderBaked,
                kitchenUserId,
                DateTime.UtcNow.AddDays(-2),
                "أربع شرائح ما تماسكت طبقاتها بعد التبريد، فتم عزلها في فحص الجودة.");
            await _wasteRepo.InsertAsync(qualityWaste, autoSave: true);
        }
    }

    private async Task RecordStockWasteAsync(
        Guid mainKitchenId,
        AppProduct product,
        int quantity,
        string wasteType,
        string wasteReason,
        Guid kitchenUserId,
        DateTime recordedAt,
        string notes)
    {
        var inventory = await _inventoryRepo.FindAsync(i =>
            i.BranchId == mainKitchenId
            && i.ProductId == product.Id)
            ?? throw new InvalidOperationException(
                $"Missing kitchen inventory for waste product '{product.SKU}'.");

        await _inventoryManager.AdjustStockAsync(
            inventory,
            inventory.QuantityOnHand - quantity,
            StockMovementTypes.ProductionWaste,
            notes,
            referenceType: "ProductionWaste");
        await _inventoryRepo.UpdateAsync(inventory);

        var waste = await _wasteManager.CreateAsync(
            productionOrderId: null,
            mainKitchenId,
            product.Id,
            wasteType,
            quantity,
            product.CostPrice,
            wasteReason,
            kitchenUserId,
            recordedAt,
            notes);
        await _wasteRepo.InsertAsync(waste, autoSave: true);
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
