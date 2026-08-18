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
    private const string CreamSweetsCategoryName = "حلويات عربية بالقشطة";
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
            "Raw Materials",
            "مواد تدخل في وصفات المطبخ الرئيسي مثل الدقيق والزبدة والسكر والبيض.",
            "Ingredients used by the central kitchen, such as flour, butter, sugar, and eggs.");
        var packagingCategory = await EnsureCategoryAsync(
            PackagingCategoryName,
            "Packaging",
            "علب وأكياس تستخدم لتجهيز الإنتاج قبل إرساله للفروع.",
            "Boxes and bags used to prepare production for branch dispatch.");
        var creamSweetsCategory = await EnsureCategoryAsync(
            CreamSweetsCategoryName,
            "Arabic Cream Sweets",
            "حلويات سورية وعربية طازجة محشوة بالقشطة ومحضّرة يومياً في المطبخ المركزي.",
            "Fresh Syrian and Arabic sweets filled with ashta and prepared daily in the central kitchen.");
        var supplier = await EnsureSupplierAsync();

        var ingredients = await EnsureIngredientProductsAsync(rawCategory.Id, packagingCategory.Id, supplier.Id);
        await EnsureKitchenIngredientStockAsync(mainKitchen.Id, ingredients);

        var finishedProducts = await EnsureFinishedProductsAreProducibleAsync();
        var creamSweets = await EnsureCreamSweetProductsAsync(creamSweetsCategory.Id);
        foreach (var (sku, product) in creamSweets)
        {
            finishedProducts[sku] = product;
        }
        await EnsureArabicFormulasAsync(finishedProducts, ingredients, kitchenUser.Id);

        var requests = await EnsureBranchRequestsAsync(finishedProducts);
        await EnsureStarterPlanAndOrdersAsync(mainKitchen.Id, requests);
        await EnsureCreamSweetsRequestsAndPlanAsync(mainKitchen.Id, finishedProducts);
        await EnsureButterShortageScenarioAsync(mainKitchen.Id, finishedProducts);
        await EnsureCompletedProductionAndWasteAsync(
            mainKitchen.Id,
            kitchenUser.Id,
            finishedProducts,
            ingredients);
    }

    private async Task<AppCategory> EnsureCategoryAsync(
        string nameAr,
        string nameEn,
        string descriptionAr,
        string descriptionEn)
    {
        var category = await _categoryRepo.FindAsync(c => c.NameAr == nameAr);
        if (category != null)
        {
            return category;
        }

        category = await _categoryManager.CreateAsync(nameAr, nameEn, descriptionAr, descriptionEn);
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
            new IngredientSpec("RM-ASHTA-001", "قشطة عربية طازجة", "غرام", rawCategoryId, 0.009m, 3, ProductTypes.RawMaterial, "قشطة طازجة لحشوات الحلويات العربية اليومية."),
            new IngredientSpec("RM-MILK-001", "حليب كامل الدسم", "مل", rawCategoryId, 0.0018m, 5, ProductTypes.RawMaterial, "حليب طازج لتحضير القشطة والحشوات."),
            new IngredientSpec("RM-SEMOLINA-001", "سميد ناعم", "غرام", rawCategoryId, 0.002m, 365, ProductTypes.RawMaterial, "سميد ناعم لحلاوة الجبن والمدلوقة."),
            new IngredientSpec("RM-AKKAWI-001", "جبنة عكاوي محلاة", "غرام", rawCategoryId, 0.01m, 14, ProductTypes.RawMaterial, "جبنة عكاوي منقوعة ومخففة الملوحة لحلاوة الجبن والكنافة."),
            new IngredientSpec("RM-PHYLLO-001", "رقائق عجينة جلاش", "غرام", rawCategoryId, 0.004m, 90, ProductTypes.RawMaterial, "رقائق جلاش لزنود الست والوربات والشعيبيات."),
            new IngredientSpec("RM-KATAIFI-001", "عجينة كنافة خشنة", "غرام", rawCategoryId, 0.0045m, 30, ProductTypes.RawMaterial, "عجينة كنافة طازجة للحلويات المخبوزة."),
            new IngredientSpec("RM-PISTACHIO-001", "فستق حلبي مجروش", "غرام", rawCategoryId, 0.035m, 180, ProductTypes.RawMaterial, "فستق حلبي مجروش للتزيين والحشوات."),
            new IngredientSpec("RM-WALNUT-001", "جوز مجروش", "غرام", rawCategoryId, 0.018m, 180, ProductTypes.RawMaterial, "جوز مجروش لحشوات القطايف والحلويات الموسمية."),
            new IngredientSpec("RM-STARCH-001", "نشاء ذرة", "غرام", rawCategoryId, 0.002m, 365, ProductTypes.RawMaterial, "نشاء لتثبيت القشطة والحشوات."),
            new IngredientSpec("RM-BLOSSOM-001", "ماء زهر", "مل", rawCategoryId, 0.006m, 365, ProductTypes.RawMaterial, "ماء زهر لتعطير القطر والقشطة."),
            new IngredientSpec("RM-GHEE-001", "سمنة عربية", "غرام", rawCategoryId, 0.011m, 365, ProductTypes.RawMaterial, "سمنة عربية لخبز الحلويات الشرقية وتحميرها."),
            new IngredientSpec("RM-TOAST-001", "خبز توست للحلويات", "غرام", rawCategoryId, 0.003m, 7, ProductTypes.RawMaterial, "خبز أبيض مخصص لتحضير عيش السرايا."),
            new IngredientSpec("PK-BOX-001", "علبة كرتون للحلويات", "علبة", packagingCategoryId, 0.25m, null, ProductTypes.Packaging, "علبة تغليف للطلبات الجاهزة والتحويلات.")
        };

        var englishNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RM-FLOUR-001"] = "Premium Croissant Flour",
            ["RM-BUTTER-001"] = "French Unsalted Butter",
            ["RM-SUGAR-001"] = "Fine Sugar",
            ["RM-EGG-001"] = "Fresh Eggs",
            ["RM-CHOC-001"] = "70% Dark Chocolate",
            ["RM-ALMOND-001"] = "Ground Almonds",
            ["RM-STRAW-001"] = "Fresh Strawberries",
            ["RM-YEAST-001"] = "Instant Yeast",
            ["RM-SALT-001"] = "Food-grade Salt",
            ["RM-ASHTA-001"] = "Fresh Arabic Ashta",
            ["RM-MILK-001"] = "Whole Milk",
            ["RM-SEMOLINA-001"] = "Fine Semolina",
            ["RM-AKKAWI-001"] = "Desalted Akkawi Cheese",
            ["RM-PHYLLO-001"] = "Phyllo Pastry Sheets",
            ["RM-KATAIFI-001"] = "Kataifi Dough",
            ["RM-PISTACHIO-001"] = "Crushed Aleppo Pistachios",
            ["RM-WALNUT-001"] = "Crushed Walnuts",
            ["RM-STARCH-001"] = "Cornstarch",
            ["RM-BLOSSOM-001"] = "Orange Blossom Water",
            ["RM-GHEE-001"] = "Arabic Ghee",
            ["RM-TOAST-001"] = "Dessert Toast Bread",
            ["PK-BOX-001"] = "Pastry Cardboard Box"
        };
        var englishDescriptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["RM-FLOUR-001"] = "High-protein white flour suitable for laminated dough and bread.",
            ["RM-BUTTER-001"] = "Cold butter for lamination and croissants.",
            ["RM-SUGAR-001"] = "Sugar for fillings, creams, and dough.",
            ["RM-EGG-001"] = "Fresh daily eggs for cakes, glazing, and creams.",
            ["RM-CHOC-001"] = "Chocolate for fillings and opera cake.",
            ["RM-ALMOND-001"] = "Almond powder for French pastries.",
            ["RM-STRAW-001"] = "Strawberries for tarts and fresh orders.",
            ["RM-YEAST-001"] = "Yeast for bread and daily dough.",
            ["RM-SALT-001"] = "Salt used to balance dough flavor.",
            ["RM-ASHTA-001"] = "Fresh ashta for daily Arabic sweet fillings.",
            ["RM-MILK-001"] = "Fresh whole milk for ashta and cream fillings.",
            ["RM-SEMOLINA-001"] = "Fine semolina for halawet el jibn and madlouka.",
            ["RM-AKKAWI-001"] = "Soaked, desalted Akkawi cheese for cheese sweets and knafeh.",
            ["RM-PHYLLO-001"] = "Phyllo sheets for znoud el sit, warbat, and shaibiyat.",
            ["RM-KATAIFI-001"] = "Fresh kataifi dough for baked Arabic sweets.",
            ["RM-PISTACHIO-001"] = "Crushed Aleppo pistachios for fillings and garnish.",
            ["RM-WALNUT-001"] = "Crushed walnuts for qatayef and seasonal fillings.",
            ["RM-STARCH-001"] = "Cornstarch used to stabilize ashta and cream fillings.",
            ["RM-BLOSSOM-001"] = "Orange blossom water for syrup and ashta.",
            ["RM-GHEE-001"] = "Arabic ghee for baking and browning traditional sweets.",
            ["RM-TOAST-001"] = "White bread prepared specifically for eish el saraya.",
            ["PK-BOX-001"] = "Packaging box for prepared orders and transfers."
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
                    englishNames[spec.Sku],
                    spec.Sku,
                    spec.Unit,
                    spec.Unit == "علبة" ? "box" : spec.Unit == "حبة" ? "piece" : spec.Unit == "مل" ? "ml" : "gram",
                    supplierId,
                    spec.Description,
                    englishDescriptions[spec.Sku],
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
            ["RM-ASHTA-001"] = 35_000,
            ["RM-MILK-001"] = 30_000,
            ["RM-SEMOLINA-001"] = 18_000,
            ["RM-AKKAWI-001"] = 22_000,
            ["RM-PHYLLO-001"] = 20_000,
            ["RM-KATAIFI-001"] = 20_000,
            ["RM-PISTACHIO-001"] = 10_000,
            ["RM-WALNUT-001"] = 10_000,
            ["RM-STARCH-001"] = 8_000,
            ["RM-BLOSSOM-001"] = 5_000,
            ["RM-GHEE-001"] = 15_000,
            ["RM-TOAST-001"] = 12_000,
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
        var targetSkus = new[] { "VN-001", "VN-002", "CT-001", "CT-002", "BR-001", "SS-002" };
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

    private async Task<Dictionary<string, AppProduct>> EnsureCreamSweetProductsAsync(Guid categoryId)
    {
        var specs = new[]
        {
            new CreamSweetSpec("AS-001", "حلاوة الجبن بالقشطة", "Halawet El Jibn with Ashta", 1.65m, 4.75m, 2, "لفائف حلاوة الجبن الطازجة محشوة بالقشطة ومزيّنة بالفستق الحلبي.", "Fresh sweet-cheese rolls filled with ashta and garnished with Aleppo pistachios."),
            new CreamSweetSpec("AS-002", "زنود الست بالقشطة", "Znoud El Sit with Ashta", 1.45m, 4.25m, 2, "رقائق مقرمشة محشوة بالقشطة ومغطاة بالقطر والفستق.", "Crisp phyllo rolls filled with ashta and finished with syrup and pistachios."),
            new CreamSweetSpec("AS-003", "وربات بالقشطة", "Warbat with Ashta", 1.35m, 4.00m, 2, "وربات جلاش مخبوزة بالسمنة ومحشوة بالقشطة الطازجة.", "Ghee-baked phyllo parcels filled with fresh ashta."),
            new CreamSweetSpec("AS-004", "مدلوقة بالقشطة والفستق", "Madlouka with Ashta and Pistachios", 1.75m, 5.00m, 3, "طبقة سميد طرية مع القشطة والفستق الحلبي.", "Soft semolina sweet layered with ashta and Aleppo pistachios."),
            new CreamSweetSpec("AS-005", "كنافة بالقشطة", "Knafeh with Ashta", 1.60m, 4.75m, 2, "كنافة خشنة مخبوزة بالسمنة ومحشوة بالقشطة.", "Ghee-baked kataifi pastry filled with fresh ashta."),
            new CreamSweetSpec("AS-006", "قطايف بالقشطة", "Qatayef with Ashta", 1.10m, 3.50m, 2, "قطايف طرية محشوة بالقشطة ومزيّنة بالفستق.", "Soft qatayef filled with ashta and garnished with pistachios."),
            new CreamSweetSpec("AS-007", "عيش السرايا بالقشطة", "Eish El Saraya with Ashta", 1.50m, 4.50m, 3, "خبز محمّص بالقطر تعلوه طبقة قشطة وفستق.", "Syrup-soaked toasted bread topped with ashta and pistachios."),
            new CreamSweetSpec("AS-008", "شعيبيات بالقشطة", "Shaibiyat with Ashta", 1.40m, 4.25m, 2, "شعيبيات جلاش ذهبية محشوة بالقشطة ومطيّبة بماء الزهر.", "Golden phyllo shaibiyat filled with ashta and scented with orange blossom water.")
        };

        var products = new Dictionary<string, AppProduct>(StringComparer.OrdinalIgnoreCase);
        foreach (var spec in specs)
        {
            var product = await _productRepo.FindAsync(p => p.SKU == spec.Sku);
            if (product == null)
            {
                product = await _productManager.CreateAsync(
                    categoryId,
                    spec.NameAr,
                    spec.NameEn,
                    spec.Sku,
                    "قطعة",
                    "piece",
                    defaultSupplierId: null,
                    spec.DescriptionAr,
                    spec.DescriptionEn,
                    spec.CostPrice,
                    spec.SalePrice,
                    currency: "USD",
                    reorderLevel: 12,
                    shelfLifeDays: spec.ShelfLifeDays,
                    productType: ProductTypes.FinishedGood,
                    isSellable: true,
                    isPurchasable: false,
                    isProducible: true);
                await _productRepo.InsertAsync(product, autoSave: true);
            }
            else if (!product.IsProducible || product.ProductType != ProductTypes.FinishedGood)
            {
                product.SetClassification(ProductTypes.FinishedGood, isSellable: true, isPurchasable: false, isProducible: true);
                await _productRepo.UpdateAsync(product, autoSave: true);
            }

            products[spec.Sku] = product;
        }

        return products;
    }

    private async Task EnsureArabicFormulasAsync(
        Dictionary<string, AppProduct> finishedProducts,
        Dictionary<string, AppProduct> ingredients,
        Guid kitchenUserId)
    {
        var formulaSpecs = new[]
        {
            new FormulaSpec(
                "VN-001",
                "وصفة كرواسون الجبنة - دفعة 50 قطعة",
                50,
                4m,
                18m,
                7m,
                180,
                "عجين مورق محشو بجبنة عكاوي محلاة، مع راحة باردة ثم خبز وتبريد قبل التحويل للفروع.",
                new[]
                {
                    Item("RM-FLOUR-001", 5_000, 2m),
                    Item("RM-BUTTER-001", 2_500, 3m),
                    Item("RM-AKKAWI-001", 1_500, 2m),
                    Item("RM-SUGAR-001", 350, 1m),
                    Item("RM-EGG-001", 20, 0m),
                    Item("RM-YEAST-001", 120, 0m),
                    Item("RM-SALT-001", 80, 0m)
                }),
            new FormulaSpec(
                "VN-002",
                "وصفة كرواسون الشوكولا - دفعة 40 قطعة",
                40,
                4.5m,
                20m,
                8m,
                190,
                "دفعة كرواسون بالشوكولا مع ضبط وزن الحشوة حتى لا يزيد الهدر.",
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
                "وصفة كاتو الشوكولا - دفعة 16 شريحة",
                16,
                5m,
                18m,
                7m,
                150,
                "قالب كاتو شوكولا يبرد جيداً قبل تقسيمه إلى شرائح متساوية.",
                new[]
                {
                    Item("RM-FLOUR-001", 1_600, 1m),
                    Item("RM-BUTTER-001", 800, 2m),
                    Item("RM-SUGAR-001", 900, 1m),
                    Item("RM-EGG-001", 28, 0m),
                    Item("RM-CHOC-001", 1_800, 2m),
                    Item("RM-MILK-001", 1_200, 1m)
                }),
            new FormulaSpec(
                "CT-002",
                "وصفة كاتو الفانيلا والفواكه - دفعة 16 شريحة",
                16,
                5m,
                22m,
                9m,
                160,
                "كاتو فانيلا بطبقات قشطة وفراولة طازجة، يجهز ويبرد قبل التقطيع.",
                new[]
                {
                    Item("RM-FLOUR-001", 1_500, 1m),
                    Item("RM-BUTTER-001", 900, 2m),
                    Item("RM-SUGAR-001", 800, 1m),
                    Item("RM-EGG-001", 30, 0m),
                    Item("RM-MILK-001", 1_200, 1m),
                    Item("RM-ASHTA-001", 1_400, 3m),
                    Item("RM-STRAW-001", 1_200, 4m)
                }),
            new FormulaSpec(
                "BR-001",
                "وصفة خبز الصمون - دفعة 80 رغيف",
                80,
                3m,
                12m,
                4m,
                150,
                "دفعة صمون يومية تعتمد على الدقيق والخميرة مع كمية حليب خفيفة لقوام طري.",
                new[]
                {
                    Item("RM-FLOUR-001", 9_000, 1m),
                    Item("RM-YEAST-001", 220, 0m),
                    Item("RM-SALT-001", 160, 0m),
                    Item("RM-SUGAR-001", 200, 0m),
                    Item("RM-MILK-001", 1_500, 1m)
                }),
            new FormulaSpec(
                "SS-002",
                "وصفة معروك رمضان بالقشطة - دفعة 30 قطعة",
                30, 6m, 17m, 7m, 125,
                "عجن المعروك وتركه يتخمر، الخبز حتى اللون الذهبي، ثم الحشو بالقشطة بعد أن يبرد.",
                new[]
                {
                    Item("RM-FLOUR-001", 2_500, 2m),
                    Item("RM-YEAST-001", 60, 0m),
                    Item("RM-SUGAR-001", 500, 1m),
                    Item("RM-BUTTER-001", 400, 2m),
                    Item("RM-MILK-001", 1_200, 1m),
                    Item("RM-ASHTA-001", 1_500, 4m),
                    Item("RM-BLOSSOM-001", 80, 1m)
                }),
            new FormulaSpec(
                "AS-001",
                "وصفة حلاوة الجبن بالقشطة - دفعة 30 قطعة",
                30, 6m, 20m, 8m, 150,
                "تحضير عجينة الجبن والسميد، فردها ولفها بالقشطة، ثم التقطيع والتزيين بالفستق.",
                new[]
                {
                    Item("RM-AKKAWI-001", 1_800, 3m),
                    Item("RM-SEMOLINA-001", 800, 2m),
                    Item("RM-SUGAR-001", 700, 1m),
                    Item("RM-ASHTA-001", 1_500, 4m),
                    Item("RM-PISTACHIO-001", 250, 2m),
                    Item("RM-BLOSSOM-001", 120, 1m)
                }),
            new FormulaSpec(
                "AS-002",
                "وصفة زنود الست بالقشطة - دفعة 30 قطعة",
                30, 7m, 18m, 7m, 120,
                "لف رقائق الجلاش حول القشطة، القلي أو الخبز حتى اللون الذهبي، ثم إضافة القطر والفستق.",
                new[]
                {
                    Item("RM-PHYLLO-001", 1_500, 5m),
                    Item("RM-ASHTA-001", 1_800, 4m),
                    Item("RM-GHEE-001", 500, 2m),
                    Item("RM-SUGAR-001", 900, 1m),
                    Item("RM-PISTACHIO-001", 200, 2m),
                    Item("RM-BLOSSOM-001", 100, 1m)
                }),
            new FormulaSpec(
                "AS-003",
                "وصفة وربات بالقشطة - دفعة 24 قطعة",
                24, 6m, 17m, 7m, 110,
                "تشكيل طبقات الجلاش بالسمنة، خبزها، ثم حشوها بالقشطة بعد أن تبرد جزئياً.",
                new[]
                {
                    Item("RM-PHYLLO-001", 1_200, 5m),
                    Item("RM-ASHTA-001", 1_600, 4m),
                    Item("RM-GHEE-001", 450, 2m),
                    Item("RM-SUGAR-001", 750, 1m),
                    Item("RM-PISTACHIO-001", 180, 2m),
                    Item("RM-BLOSSOM-001", 80, 1m)
                }),
            new FormulaSpec(
                "AS-004",
                "وصفة مدلوقة بالقشطة والفستق - دفعة 20 قطعة",
                20, 5m, 16m, 6m, 100,
                "طهي السميد بالسمنة والقطر، فرده في الصواني، ثم إضافة القشطة والفستق بعد التبريد.",
                new[]
                {
                    Item("RM-SEMOLINA-001", 1_000, 2m),
                    Item("RM-ASHTA-001", 1_600, 4m),
                    Item("RM-SUGAR-001", 800, 1m),
                    Item("RM-GHEE-001", 250, 2m),
                    Item("RM-PISTACHIO-001", 400, 2m),
                    Item("RM-BLOSSOM-001", 100, 1m)
                }),
            new FormulaSpec(
                "AS-005",
                "وصفة كنافة بالقشطة - دفعة 20 قطعة",
                20, 7m, 20m, 8m, 130,
                "توزيع عجينة الكنافة بالسمنة، إضافة القشطة، الخبز حتى اللون الذهبي ثم التشريب بالقطر.",
                new[]
                {
                    Item("RM-KATAIFI-001", 2_000, 5m),
                    Item("RM-ASHTA-001", 1_800, 4m),
                    Item("RM-GHEE-001", 650, 2m),
                    Item("RM-SUGAR-001", 900, 1m),
                    Item("RM-PISTACHIO-001", 250, 2m),
                    Item("RM-BLOSSOM-001", 100, 1m)
                }),
            new FormulaSpec(
                "AS-006",
                "وصفة قطايف بالقشطة - دفعة 30 قطعة",
                30, 6m, 16m, 6m, 100,
                "تحضير عجينة القطايف وتركها تتخمر، خبز الأقراص من جهة واحدة، ثم الحشو بالقشطة والتزيين.",
                new[]
                {
                    Item("RM-FLOUR-001", 1_800, 2m),
                    Item("RM-YEAST-001", 30, 0m),
                    Item("RM-SUGAR-001", 650, 1m),
                    Item("RM-MILK-001", 1_200, 1m),
                    Item("RM-ASHTA-001", 1_500, 4m),
                    Item("RM-PISTACHIO-001", 150, 2m),
                    Item("RM-BLOSSOM-001", 80, 1m)
                }),
            new FormulaSpec(
                "AS-007",
                "وصفة عيش السرايا بالقشطة - دفعة 20 قطعة",
                20, 5m, 15m, 6m, 90,
                "تحميص الخبز وتشريبه بالقطر، تبريده ثم تغطيته بالقشطة والفستق.",
                new[]
                {
                    Item("RM-TOAST-001", 1_600, 3m),
                    Item("RM-SUGAR-001", 1_200, 1m),
                    Item("RM-ASHTA-001", 1_800, 4m),
                    Item("RM-PISTACHIO-001", 250, 2m),
                    Item("RM-BLOSSOM-001", 120, 1m)
                }),
            new FormulaSpec(
                "AS-008",
                "وصفة شعيبيات بالقشطة - دفعة 30 قطعة",
                30, 6m, 18m, 7m, 120,
                "طي رقائق الجلاش بشكل مثلثات، حشوها بالقشطة، خبزها بالسمنة ثم إضافة القطر.",
                new[]
                {
                    Item("RM-PHYLLO-001", 1_500, 5m),
                    Item("RM-ASHTA-001", 1_700, 4m),
                    Item("RM-GHEE-001", 500, 2m),
                    Item("RM-SUGAR-001", 850, 1m),
                    Item("RM-PISTACHIO-001", 200, 2m),
                    Item("RM-BLOSSOM-001", 90, 1m)
                })
        };

        foreach (var spec in formulaSpecs)
        {
            if (!finishedProducts.TryGetValue(spec.FinishedSku, out var finishedProduct))
            {
                continue;
            }

            var activeFormulas = await _formulaRepo.GetListAsync(f =>
                f.FinishedProductId == finishedProduct.Id && f.IsActive);
            var desiredFormula = activeFormulas.FirstOrDefault(f => f.FormulaName == spec.Name);
            if (desiredFormula != null)
            {
                desiredFormula = await _formulaRepo.GetWithItemsAsync(desiredFormula.Id);
                foreach (var otherDefault in activeFormulas.Where(f => f.Id != desiredFormula.Id && f.IsDefault))
                {
                    otherDefault.UnmarkDefault();
                    await _formulaRepo.UpdateAsync(otherDefault);
                }

                if (desiredFormula.ApprovalStatus == ProductionFormulaStatuses.Draft)
                {
                    desiredFormula.Approve(kitchenUserId, DateTime.UtcNow);
                }

                if (!desiredFormula.IsDefault)
                {
                    desiredFormula.MarkDefault();
                }

                await _formulaRepo.UpdateAsync(desiredFormula, autoSave: true);
                continue;
            }

            var nextVersion = activeFormulas.Count == 0
                ? 1
                : activeFormulas.Max(f => f.Version) + 1;

            await _formulaManager.EnsureIngredientsAreValidAsync(
                spec.Items.Select(i => ingredients[i.IngredientSku].Id));

            var formula = await _formulaManager.CreateAsync(
                finishedProduct.Id,
                spec.Name,
                spec.OutputQuantity,
                version: nextVersion,
                spec.ExpectedWastePercent,
                spec.LaborCostPerBatch,
                spec.OverheadCostPerBatch,
                spec.EstimatedProductionMinutes,
                isActive: true,
                isDefault: true,
                notes: spec.Notes);

            AddFormulaItems(formula, spec, ingredients);

            formula.Approve(kitchenUserId, DateTime.UtcNow);
            formula.MarkDefault();

            await _formulaRepo.InsertAsync(formula, autoSave: true);
        }
    }

    private static void AddFormulaItems(
        AppProductionFormula formula,
        FormulaSpec spec,
        Dictionary<string, AppProduct> ingredients)
    {
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
                "طلب نهاية الأسبوع مع تركيز على شرائح الكاتو بالشوكولا والفواكه.",
                new Dictionary<string, int>
                {
                    ["CT-001"] = 18,
                    ["CT-002"] = 20,
                    ["VN-002"] = 24
                }),
            new BranchRequestSpec(
                "فرع الشعلان",
                ProductionPriorities.Urgent,
                "الكمية المتوفرة لا تكفي حركة يوم الجمعة؛ الأولوية للكرواسون والصمون.",
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
            var branch = await _branchRepo.FindAsync(b => b.NameAr == spec.BranchName);
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

    private async Task EnsureCreamSweetsRequestsAndPlanAsync(
        Guid mainKitchenId,
        Dictionary<string, AppProduct> finishedProducts)
    {
        var neededByDate = DateTime.UtcNow.Date.AddDays(3);
        var specs = new[]
        {
            new BranchRequestSpec(
                "فرع المزة",
                ProductionPriorities.Urgent,
                "برنامج الحلويات العربية بالقشطة - تشكيلة المزة للعرض المسائي.",
                new Dictionary<string, int> { ["AS-001"] = 30, ["AS-002"] = 30, ["SS-002"] = 30 }),
            new BranchRequestSpec(
                "فرع المالكي",
                ProductionPriorities.Normal,
                "برنامج الحلويات العربية بالقشطة - تشكيلة المالكي للضيافة والطلبات.",
                new Dictionary<string, int> { ["AS-003"] = 24, ["AS-004"] = 20 }),
            new BranchRequestSpec(
                "فرع أبو رمانة",
                ProductionPriorities.Urgent,
                "برنامج الحلويات العربية بالقشطة - تشكيلة أبو رمانة الطازجة.",
                new Dictionary<string, int> { ["AS-005"] = 20, ["AS-006"] = 30 }),
            new BranchRequestSpec(
                "فرع باب توما",
                ProductionPriorities.Normal,
                "برنامج الحلويات العربية بالقشطة - تشكيلة باب توما للعرض اليومي.",
                new Dictionary<string, int> { ["AS-007"] = 20, ["AS-008"] = 30 })
        };

        foreach (var spec in specs)
        {
            if (await _requestRepo.AnyAsync(r => r.Notes == spec.Notes))
            {
                continue;
            }

            var branch = await _branchRepo.FindAsync(b => b.NameAr == spec.BranchName);
            if (branch == null)
            {
                _logger.LogWarning("Cream-sweets production request branch {BranchName} was not found.", spec.BranchName);
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

            var selectedProducts = spec.Quantities
                .Where(x => finishedProducts.ContainsKey(x.Key))
                .Select(x => finishedProducts[x.Key])
                .ToList();
            await _requestManager.EnsureRequestProductsAreProducibleAsync(selectedProducts.Select(p => p.Id));

            var request = await _requestManager.CreateAsync(
                branch.Id,
                neededByDate,
                spec.Priority,
                managerUser?.Id,
                spec.Notes);

            foreach (var (sku, quantity) in spec.Quantities)
            {
                if (finishedProducts.TryGetValue(sku, out var product))
                {
                    request.AddItem(
                        Guid.NewGuid(),
                        product.Id,
                        quantity,
                        "كمية دفعة كاملة حتى تصل الحلويات طازجة وبنفس مستوى الجودة إلى الفرع.");
                }
            }

            request.Submit();
            request.Approve(
                managerUser?.Id,
                request.Items.ToDictionary(i => i.Id, i => i.RequestedQuantity),
                "تم اعتماد دفعات الحلويات بالقشطة حسب الطاقة اليومية ومدة الصلاحية القصيرة.");
            await _requestRepo.InsertAsync(request, autoSave: true);
        }

        if (await _planRepo.AnyAsync(p =>
                p.KitchenBranchId == mainKitchenId
                && p.ProductionDate == neededByDate))
        {
            return;
        }

        var kitchenUser = await _userManager.FindByNameAsync(IdentityDataSeedContributor.KitchenManagerUserName);
        var plan = await _planManager.CreateDraftAsync(
            mainKitchenId,
            neededByDate,
            kitchenUser?.Id,
            "خطة الحلويات العربية بالقشطة: تبدأ بتحضير القشطة والقطر، ثم الأصناف المخبوزة، وتُنهي بالأصناف الباردة قبل التوزيع.");

        if (plan.Lines.Count == 0)
        {
            _logger.LogWarning("The cream-sweets production plan has no approved request lines.");
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
                    "انشال هالصنف من خطة الحلويات لأنه ما عنده وصفة إنتاج افتراضية معتمدة.");
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

        var branch = await _branchRepo.FindAsync(b => b.NameAr == "فرع جرمانا");
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

        if (finishedProducts.TryGetValue("BR-001", out var samoon))
        {
            await RecordStockWasteAsync(
                mainKitchenId,
                samoon,
                quantity: 6,
                ProductionWasteTypes.ExpiredFinishedGood,
                ProductionWasteReasons.ExpiredBeforeDispatch,
                kitchenUserId,
                DateTime.UtcNow.AddDays(-5),
                "ستة أرغفة بقيت بعد إلغاء رحلة التوزيع المسائية وانتهت صلاحيتها قبل الإرسال.");
        }

        if (finishedProducts.TryGetValue("CT-002", out var vanillaFruitGateau))
        {
            var qualityWaste = await _wasteManager.CreateAsync(
                productionOrderId: null,
                mainKitchenId,
                vanillaFruitGateau.Id,
                ProductionWasteTypes.RejectedOutput,
                quantity: 4,
                vanillaFruitGateau.CostPrice,
                ProductionWasteReasons.UnderBaked,
                kitchenUserId,
                DateTime.UtcNow.AddDays(-2),
                "أربع شرائح كاتو ما تماسكت طبقات القشطة والفواكه فيها بعد التبريد، فتم عزلها في فحص الجودة.");
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

    private sealed record CreamSweetSpec(
        string Sku,
        string NameAr,
        string NameEn,
        decimal CostPrice,
        decimal SalePrice,
        int ShelfLifeDays,
        string DescriptionAr,
        string DescriptionEn);

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
