using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Entities;
using Intelligence;
using Intelligence.Decisions;
using Intelligence.Rules;
using Intelligence.Services;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Entities;
using Inventory.Stocktakes;
using Inventory.Events;
using Operations;
using Operations.Cashiers;
using Operations.Entities;
using Operations.PurchaseOrders;
using Operations.StockTransfers;
using Production.Entities;
using Production.Orders;
using Production.Waste;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Security.Claims;
using Volo.Abp.Uow;

namespace patisserie_shop.DbMigrator;

/// <summary>
/// Adds the operational snapshots needed for a graduation presentation: purchase orders in
/// different states, transfers at every stage, cashier drawer sessions, and a submitted
/// stocktake waiting for manager review. All records use ordinary Arabic business notes and
/// real presentation accounts.
/// </summary>
public class GraduationOperationsSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly PurchaseOrderManager _purchaseOrderManager;
    private readonly StockTransferManager _stockTransferManager;
    private readonly BranchInventoryManager _inventoryManager;
    private readonly CashierShiftManager _cashierShiftManager;
    private readonly StocktakeSessionManager _stocktakeSessionManager;
    private readonly DecisionMakerService _decisionMakerService;
    private readonly IPurchaseOrderRepository _purchaseOrderRepository;
    private readonly IStockTransferRepository _stockTransferRepository;
    private readonly ICashierShiftRepository _cashierShiftRepository;
    private readonly IStocktakeSessionRepository _stocktakeSessionRepository;
    private readonly IRepository<AppSupplier, Guid> _supplierRepository;
    private readonly IRepository<AppProduct, Guid> _productRepository;
    private readonly IRepository<AppBranch, Guid> _branchRepository;
    private readonly IRepository<AppBranchInventory, Guid> _inventoryRepository;
    private readonly IRepository<AppSale, Guid> _saleRepository;
    private readonly IRepository<AppDecisionLog, Guid> _decisionLogRepository;
    private readonly IRepository<AppInventoryRule, Guid> _inventoryRuleRepository;
    private readonly IProductionOrderRepository _productionOrderRepository;
    private readonly IProductionWasteRepository _productionWasteRepository;
    private readonly IdentityUserManager _userManager;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ILogger<GraduationOperationsSeedContributor> _logger;

    public GraduationOperationsSeedContributor(
        PurchaseOrderManager purchaseOrderManager,
        StockTransferManager stockTransferManager,
        BranchInventoryManager inventoryManager,
        CashierShiftManager cashierShiftManager,
        StocktakeSessionManager stocktakeSessionManager,
        DecisionMakerService decisionMakerService,
        IPurchaseOrderRepository purchaseOrderRepository,
        IStockTransferRepository stockTransferRepository,
        ICashierShiftRepository cashierShiftRepository,
        IStocktakeSessionRepository stocktakeSessionRepository,
        IRepository<AppSupplier, Guid> supplierRepository,
        IRepository<AppProduct, Guid> productRepository,
        IRepository<AppBranch, Guid> branchRepository,
        IRepository<AppBranchInventory, Guid> inventoryRepository,
        IRepository<AppSale, Guid> saleRepository,
        IRepository<AppDecisionLog, Guid> decisionLogRepository,
        IRepository<AppInventoryRule, Guid> inventoryRuleRepository,
        IProductionOrderRepository productionOrderRepository,
        IProductionWasteRepository productionWasteRepository,
        IdentityUserManager userManager,
        ICurrentPrincipalAccessor currentPrincipalAccessor,
        IGuidGenerator guidGenerator,
        ILogger<GraduationOperationsSeedContributor> logger)
    {
        _purchaseOrderManager = purchaseOrderManager;
        _stockTransferManager = stockTransferManager;
        _inventoryManager = inventoryManager;
        _cashierShiftManager = cashierShiftManager;
        _stocktakeSessionManager = stocktakeSessionManager;
        _decisionMakerService = decisionMakerService;
        _purchaseOrderRepository = purchaseOrderRepository;
        _stockTransferRepository = stockTransferRepository;
        _cashierShiftRepository = cashierShiftRepository;
        _stocktakeSessionRepository = stocktakeSessionRepository;
        _supplierRepository = supplierRepository;
        _productRepository = productRepository;
        _branchRepository = branchRepository;
        _inventoryRepository = inventoryRepository;
        _saleRepository = saleRepository;
        _decisionLogRepository = decisionLogRepository;
        _inventoryRuleRepository = inventoryRuleRepository;
        _productionOrderRepository = productionOrderRepository;
        _productionWasteRepository = productionWasteRepository;
        _userManager = userManager;
        _currentPrincipalAccessor = currentPrincipalAccessor;
        _guidGenerator = guidGenerator;
        _logger = logger;
    }

    [UnitOfWork]
    public async Task SeedAsync(DataSeedContext context)
    {
        var branches = (await _branchRepository.GetListAsync()).ToDictionary(x => x.NameAr);
        var products = (await _productRepository.GetListAsync()).ToDictionary(
            x => x.SKU,
            StringComparer.OrdinalIgnoreCase);
        var suppliers = await _supplierRepository.GetListAsync();
        var admin = await _userManager.FindByNameAsync("admin")
            ?? throw new InvalidOperationException("The built-in admin account was not created.");

        await EnsurePurchaseOrdersAsync(branches, products, suppliers, admin);
        await EnsureTransfersAsync(branches, products, admin);
        await EnsureCashierShiftsAsync(branches);
        await EnsurePendingStocktakeAsync(branches);
        await EnsureDecisionLogsAsync(branches, products, admin);
        await VerifyAndLogAsync(branches, admin);
    }

    private async Task EnsurePurchaseOrdersAsync(
        Dictionary<string, AppBranch> branches,
        Dictionary<string, AppProduct> products,
        List<AppSupplier> suppliers,
        IdentityUser admin)
    {
        if (await _purchaseOrderRepository.AnyAsync())
        {
            return;
        }

        using var adminScope = _currentPrincipalAccessor.Change(
            GraduationSeedData.PrincipalFor(admin, IdentityDataSeedContributor.AdminRoleName));

        var kitchen = branches[GraduationSeedData.MainKitchenName];
        var specs = new[]
        {
            new PurchaseOrderSpec(
                PurchaseOrderStatuses.Draft,
                "طلب أسعار شهري للدقيق والسكر؛ بانتظار تأكيد المورد على موعد التسليم.",
                new Dictionary<string, int> { ["RM-FLOUR-001"] = 25_000, ["RM-SUGAR-001"] = 8_000 }),
            new PurchaseOrderSpec(
                PurchaseOrderStatuses.Submitted,
                "احتياج الأسبوع القادم من الزبدة والشوكولا، أُرسل للمراجعة المالية.",
                new Dictionary<string, int> { ["RM-BUTTER-001"] = 12_000, ["RM-CHOC-001"] = 6_000 }),
            new PurchaseOrderSpec(
                PurchaseOrderStatuses.Approved,
                "علب تغليف لأصناف العيد؛ تم اعتماد السعر وبانتظار وصول الشحنة.",
                new Dictionary<string, int> { ["PK-BOX-001"] = 500 }),
            new PurchaseOrderSpec(
                PurchaseOrderStatuses.PartialReceived,
                "وصلت الدفعة الأولى صباحاً، وباقي الكمية مع سيارة التوزيع الثانية.",
                new Dictionary<string, int> { ["RM-EGG-001"] = 240, ["RM-FLOUR-001"] = 10_000 }),
            new PurchaseOrderSpec(
                PurchaseOrderStatuses.Received,
                "توريد الفراولة الصباحية وصل كاملاً وتم فحص الجودة والحرارة.",
                new Dictionary<string, int> { ["RM-STRAW-001"] = 4_000 }),
            new PurchaseOrderSpec(
                PurchaseOrderStatuses.Cancelled,
                "أُلغي الطلب بعد تعديل خطة الإنتاج وتوفر الكمية من مورد بديل.",
                new Dictionary<string, int> { ["RM-ALMOND-001"] = 3_000 })
        };

        foreach (var spec in specs)
        {
            var firstProduct = products[spec.Quantities.Keys.First()];
            var supplier = firstProduct.DefaultSupplierId.HasValue
                ? suppliers.First(x => x.Id == firstProduct.DefaultSupplierId.Value)
                : suppliers[0];
            var purchaseOrder = await _purchaseOrderManager.CreateDraftAsync(
                supplier.Id,
                kitchen.Id,
                DateTime.UtcNow.Date.AddDays(-6),
                DateTime.UtcNow.Date.AddDays(2),
                "USD",
                spec.Notes);

            foreach (var (sku, quantity) in spec.Quantities)
            {
                var product = products[sku];
                purchaseOrder.AddItem(
                    _guidGenerator.Create(),
                    product.Id,
                    quantity,
                    product.CostPrice);
            }

            if (spec.Status != PurchaseOrderStatuses.Draft)
            {
                purchaseOrder.Submit();
            }
            if (spec.Status is PurchaseOrderStatuses.Approved
                or PurchaseOrderStatuses.PartialReceived
                or PurchaseOrderStatuses.Received)
            {
                purchaseOrder.Approve();
            }
            if (spec.Status == PurchaseOrderStatuses.Cancelled)
            {
                purchaseOrder.Cancel();
            }
            else if (spec.Status == PurchaseOrderStatuses.PartialReceived)
            {
                var firstLine = purchaseOrder.Items.First();
                var received = Math.Max(1, firstLine.OrderedQuantity / 2);
                var applied = purchaseOrder.RecordReceipt([(firstLine.Id, received)]);
                await ApplyPurchaseReceiptsAsync(
                    purchaseOrder,
                    applied,
                    products.Values.ToDictionary(x => x.Id));
            }
            else if (spec.Status == PurchaseOrderStatuses.Received)
            {
                var applied = purchaseOrder.RecordReceipt(
                    purchaseOrder.Items.Select(x => (x.Id, x.OrderedQuantity)));
                await ApplyPurchaseReceiptsAsync(
                    purchaseOrder,
                    applied,
                    products.Values.ToDictionary(x => x.Id));
            }

            await _purchaseOrderRepository.InsertAsync(purchaseOrder, autoSave: true);
        }
    }

    private async Task ApplyPurchaseReceiptsAsync(
        AppPurchaseOrder purchaseOrder,
        IReadOnlyList<AppPurchaseOrder.ReceivedLine> lines,
        Dictionary<Guid, AppProduct> productsById)
    {
        foreach (var line in lines)
        {
            var inventory = await _inventoryRepository.FindAsync(x =>
                x.BranchId == purchaseOrder.DestBranchId
                && x.ProductId == line.ProductId);
            if (inventory == null)
            {
                inventory = await _inventoryManager.InitializeAsync(
                    purchaseOrder.DestBranchId,
                    line.ProductId);
                await _inventoryRepository.InsertAsync(inventory);
            }

            var product = productsById[line.ProductId];
            await _inventoryManager.AdjustStockAsync(
                inventory,
                inventory.QuantityOnHand + line.Delta,
                StockMovementTypes.Purchase,
                $"استلام على أمر الشراء {purchaseOrder.PONumber}.",
                purchaseOrder.Id,
                nameof(AppPurchaseOrder),
                batchExpiryDate: product.ShelfLifeDays.HasValue
                    ? DateTime.UtcNow.Date.AddDays(product.ShelfLifeDays.Value)
                    : null);
            await _inventoryRepository.UpdateAsync(inventory);
        }
    }

    private async Task EnsureTransfersAsync(
        Dictionary<string, AppBranch> branches,
        Dictionary<string, AppProduct> products,
        IdentityUser admin)
    {
        if (await _stockTransferRepository.AnyAsync())
        {
            return;
        }

        var kitchen = branches[GraduationSeedData.MainKitchenName];
        var specs = new[]
        {
            new TransferSpec(0, "VN-001", 18, StockTransferStatuses.Draft,
                "طلب أصناف فطور إضافية ليوم الخميس."),
            new TransferSpec(1, "BR-001", 24, StockTransferStatuses.Pending,
                "المخزون الصباحي لا يكفي الحجوزات الحالية."),
            new TransferSpec(2, "CT-001", 10, StockTransferStatuses.Approved,
                "كمية تارت معتمدة وبانتظار تجهيز سيارة التبريد."),
            new TransferSpec(3, "VN-002", 20, StockTransferStatuses.InTransit,
                "خرجت الشحنة من المطبخ ومن المتوقع وصولها خلال ساعة."),
            new TransferSpec(4, "CB-001", 16, StockTransferStatuses.Completed,
                "وصلت الشحنة للفرع وتم عدّ الصناديق عند الاستلام."),
            new TransferSpec(5, "CT-002", 12, StockTransferStatuses.Rejected,
                "طاقة المطبخ لهذا اليوم محجوزة لطلبات أقدم.")
        };

        foreach (var spec in specs)
        {
            var branchSpec = GraduationSeedData.RetailBranches[spec.BranchIndex];
            var destination = branches[branchSpec.Name];
            var manager = await _userManager.FindByNameAsync(branchSpec.ManagerUserName)
                ?? throw new InvalidOperationException($"Missing user '{branchSpec.ManagerUserName}'.");
            using var managerScope = _currentPrincipalAccessor.Change(
                GraduationSeedData.PrincipalFor(
                    manager,
                    IdentityDataSeedContributor.BranchManagerRoleName));

            var transfer = _stockTransferManager.CreateDraft(
                kitchen.Id,
                destination.Id,
                DateTime.UtcNow.Date,
                manager.Id,
                spec.Notes);
            var product = products[spec.Sku];
            transfer.AddItem(_guidGenerator.Create(), product.Id, spec.Quantity);

            if (spec.Status != StockTransferStatuses.Draft)
            {
                transfer.Submit();
            }
            if (spec.Status == StockTransferStatuses.Rejected)
            {
                transfer.Reject(admin.Id, "الكمية المطلوبة تتجاوز طاقة الإنتاج المتاحة لهذا اليوم.");
            }
            else if (spec.Status is StockTransferStatuses.Approved
                     or StockTransferStatuses.InTransit
                     or StockTransferStatuses.Completed)
            {
                transfer.Approve(admin.Id);
            }

            if (spec.Status is StockTransferStatuses.InTransit or StockTransferStatuses.Completed)
            {
                var shipped = transfer.Ship(shippedByUserId: admin.Id);
                foreach (var line in shipped)
                {
                    var sourceInventory = await _inventoryRepository.FindAsync(x =>
                        x.BranchId == kitchen.Id && x.ProductId == line.ProductId)
                        ?? throw new InvalidOperationException("Missing source inventory for transfer.");
                    var adjustment = await _inventoryManager.AdjustStockDetailedAsync(
                        sourceInventory,
                        sourceInventory.QuantityOnHand - line.Quantity,
                        StockMovementTypes.TransferOut,
                        $"شحن للفرع على التحويل {transfer.Id}.",
                        transfer.Id,
                        nameof(AppStockTransfer));
                    await _inventoryRepository.UpdateAsync(sourceInventory);
                    transfer.RecordItemShippedBatches(
                        line.ItemId,
                        StockTransferBatchBreakdown.Format(
                            adjustment.ConsumedBatches.Select(x =>
                                new StockTransferBatchBreakdown.Line(x.ExpiryDate, x.Quantity))));
                }
            }

            if (spec.Status == StockTransferStatuses.Completed)
            {
                var receivedByItem = transfer.Items.ToDictionary(
                    x => x.Id,
                    x => Math.Max(0, (x.ShippedQuantity ?? x.RequestedQuantity) - 1));
                var received = transfer.Complete(receivedByItem, manager.Id);
                foreach (var line in received)
                {
                    var destinationInventory = await _inventoryRepository.FindAsync(x =>
                        x.BranchId == destination.Id && x.ProductId == line.ProductId)
                        ?? throw new InvalidOperationException("Missing destination inventory for transfer.");
                    await _inventoryManager.AdjustStockAsync(
                        destinationInventory,
                        destinationInventory.QuantityOnHand + line.Quantity,
                        StockMovementTypes.TransferIn,
                        $"استلام من المطبخ على التحويل {transfer.Id}.",
                        transfer.Id,
                        nameof(AppStockTransfer));
                    await _inventoryRepository.UpdateAsync(destinationInventory);
                }
            }

            await _stockTransferRepository.InsertAsync(transfer, autoSave: true);
        }
    }

    private async Task EnsureCashierShiftsAsync(Dictionary<string, AppBranch> branches)
    {
        if (await _cashierShiftRepository.AnyAsync())
        {
            return;
        }

        for (var index = 0; index < GraduationSeedData.RetailBranches.Count; index++)
        {
            var spec = GraduationSeedData.RetailBranches[index];
            var cashier = await _userManager.FindByNameAsync(spec.CashierUserName!)
                ?? throw new InvalidOperationException($"Missing user '{spec.CashierUserName}'.");
            using var cashierScope = _currentPrincipalAccessor.Change(
                GraduationSeedData.PrincipalFor(
                    cashier,
                    IdentityDataSeedContributor.CashierRoleName));

            var closed = await _cashierShiftManager.CreateOpenAsync(
                branches[spec.Name].Id,
                cashier.Id,
                openingFloat: 100m);
            await _cashierShiftRepository.InsertAsync(closed, autoSave: true);
            var expectedCash = 320m + index * 27m;
            var variance = index % 3 switch { 0 => 0m, 1 => -1.5m, _ => 2m };
            closed.Close(expectedCash + variance, expectedCash);
            await _cashierShiftRepository.UpdateAsync(closed, autoSave: true);

            var current = await _cashierShiftManager.CreateOpenAsync(
                branches[spec.Name].Id,
                cashier.Id,
                openingFloat: 100m);
            await _cashierShiftRepository.InsertAsync(current, autoSave: true);
        }
    }

    private async Task EnsurePendingStocktakeAsync(Dictionary<string, AppBranch> branches)
    {
        if (await _stocktakeSessionRepository.AnyAsync())
        {
            return;
        }

        var spec = GraduationSeedData.RetailBranches[3];
        var manager = await _userManager.FindByNameAsync(spec.ManagerUserName)
            ?? throw new InvalidOperationException($"Missing user '{spec.ManagerUserName}'.");
        using var managerScope = _currentPrincipalAccessor.Change(
            GraduationSeedData.PrincipalFor(
                manager,
                IdentityDataSeedContributor.BranchManagerRoleName));

        var start = await _stocktakeSessionManager.StartAsync(
            branches[spec.Name].Id,
            manager.Id,
            manager.UserName);
        var session = start.Session;
        var differenceLines = session.Lines
            .Where(x => x.ExpectedQuantity >= 4)
            .Take(2)
            .ToList();
        var updates = session.Lines.Select(line =>
        {
            if (line.Id == differenceLines[0].Id)
            {
                return new StocktakeSessionDraftLine(
                    line.Id,
                    line.ExpectedQuantity - 2,
                    StocktakeVarianceReasons.Damaged,
                    "قطعتان تضررتا أثناء ترتيب واجهة العرض.",
                    null);
            }
            if (line.Id == differenceLines[1].Id)
            {
                return new StocktakeSessionDraftLine(
                    line.Id,
                    line.ExpectedQuantity - 1,
                    StocktakeVarianceReasons.UnrecordedSale,
                    "فاتورة واحدة لم تُغلق قبل بدء العد.",
                    null);
            }

            return new StocktakeSessionDraftLine(
                line.Id,
                line.ExpectedQuantity,
                null,
                null,
                null);
        }).ToList();

        await _stocktakeSessionManager.SubmitAsync(
            session,
            updates,
            "جرد نهاية الوردية؛ تم عد الواجهة والمستودع الخلفي بشكل منفصل.",
            session.ConcurrencyStamp,
            manager.Id,
            manager.UserName);
        await _stocktakeSessionRepository.InsertAsync(session, autoSave: true);
    }

    private async Task VerifyAndLogAsync(
        Dictionary<string, AppBranch> branches,
        IdentityUser admin)
    {
        if (branches.Count != GraduationSeedData.RetailBranches.Count + 1)
        {
            throw new InvalidOperationException(
                $"Expected 9 presentation sites but found {branches.Count}.");
        }

        var verifiedAccounts = 0;
        foreach (var spec in GraduationSeedData.Users())
        {
            var user = await _userManager.FindByNameAsync(spec.UserName)
                ?? throw new InvalidOperationException($"Missing presentation account '{spec.UserName}'.");
            if (!await _userManager.CheckPasswordAsync(user, GraduationSeedData.Password))
            {
                throw new InvalidOperationException(
                    $"Presentation account '{spec.UserName}' does not use the shared password.");
            }

            verifiedAccounts++;
        }
        if (!await _userManager.CheckPasswordAsync(admin, GraduationSeedData.Password))
        {
            throw new InvalidOperationException("The admin account does not use the shared presentation password.");
        }
        verifiedAccounts++;

        foreach (var branchSpec in GraduationSeedData.RetailBranches)
        {
            var cashier = await _userManager.FindByNameAsync(branchSpec.CashierUserName!);
            var assignedBranchId = (await _userManager.GetClaimsAsync(cashier!))
                .SingleOrDefault(x => x.Type == CashierClaimTypes.AssignedBranchId)
                ?.Value;
            if (assignedBranchId != branches[branchSpec.Name].Id.ToString())
            {
                throw new InvalidOperationException(
                    $"Cashier '{branchSpec.CashierUserName}' is not assigned to '{branchSpec.Name}'.");
            }
        }

        var sales = await _saleRepository.GetListAsync();
        if (sales.Any(x => !x.CreatorId.HasValue))
        {
            throw new InvalidOperationException("One or more historical sales are missing their cashier attribution.");
        }

        var purchaseOrders = await _purchaseOrderRepository.GetListAsync();
        if (purchaseOrders.Any(x => x.CreatorId != admin.Id))
        {
            throw new InvalidOperationException("One or more purchase orders are not attributed to admin.");
        }

        var transfers = await _stockTransferRepository.GetListAsync();
        var shifts = await _cashierShiftRepository.GetListAsync();
        var stocktakes = await _stocktakeSessionRepository.GetListAsync();
        var productionOrders = await _productionOrderRepository.GetListAsync();
        var wastes = await _productionWasteRepository.GetListAsync();
        var decisions = await _decisionLogRepository.GetListAsync();

        _logger.LogInformation(
            "[Presentation check] Accounts={Accounts}; Sites={Sites}; Sales={Sales}; " +
            "PurchaseOrders={PurchaseOrders} ({PurchaseStatuses}); Transfers={Transfers} ({TransferStatuses}); " +
            "CashierShifts={Shifts}; Stocktakes={Stocktakes}; ProductionOrders={ProductionOrders} ({ProductionStatuses}); " +
            "WasteRecords={Waste}; DecisionLogs={Decisions}.",
            verifiedAccounts,
            branches.Count,
            sales.Count,
            purchaseOrders.Count,
            StatusSummary(purchaseOrders.Select(x => x.Status)),
            transfers.Count,
            StatusSummary(transfers.Select(x => x.Status)),
            shifts.Count,
            stocktakes.Count,
            productionOrders.Count,
            StatusSummary(productionOrders.Select(x => x.Status)),
            wastes.Count,
            decisions.Count);
    }

    private async Task EnsureDecisionLogsAsync(
        Dictionary<string, AppBranch> branches,
        Dictionary<string, AppProduct> products,
        IdentityUser admin)
    {
        if (await _decisionLogRepository.AnyAsync())
        {
            return;
        }

        using var adminScope = _currentPrincipalAccessor.Change(
            GraduationSeedData.PrincipalFor(admin, IdentityDataSeedContributor.AdminRoleName));

        var inventories = await _inventoryRepository.GetListAsync();
        foreach (var inventory in inventories.Where(x =>
                     x.QuantityOnHand < 5 || x.QuantityOnHand > 100))
        {
            await _decisionMakerService.EvaluateAsync(new StockChangedEto
            {
                BranchId = inventory.BranchId,
                ProductId = inventory.ProductId,
                OldQty = inventory.QuantityOnHand + 1,
                NewQty = inventory.QuantityOnHand
            });
        }

        var rules = await _inventoryRuleRepository.GetListAsync();
        var mezza = branches["فرع المزة"];
        var malki = branches["فرع المالكي"];
        var babTouma = branches["فرع باب توما"];
        var kitchen = branches[GraduationSeedData.MainKitchenName];

        var deadStock = NewDecision(
            rules,
            InventoryRuleTypes.DeadStock,
            products["SS-001"].Id,
            mezza.Id,
            DecisionTypes.DeadStockFlag,
            "علبة المعمول موجودة بفرع المزة من 41 يوم وما انباعت منها أي قطعة خلال هالفترة.",
            "اعمل عرض على الكمية أو انقلها لفرع مبيعاته الموسمية أسرع.",
            stockAtEvaluation: 8,
            daysWithoutSale: 41);
        await _decisionLogRepository.InsertAsync(deadStock, autoSave: true);

        var transfer = NewDecision(
            rules,
            InventoryRuleTypes.TransferSuggestion,
            products["BR-003"].Id,
            babTouma.Id,
            DecisionTypes.TransferSuggestion,
            "فرع باب توما بقي عنده 4 أرغفة، بينما المطبخ المركزي عنده كمية كافية للتوزيع.",
            "حوّل 18 رغيف من المطبخ المركزي لفرع باب توما قبل وردية المساء.",
            stockAtEvaluation: 4,
            sourceBranchId: kitchen.Id,
            targetBranchId: babTouma.Id);
        transfer.MarkExecuted(admin.Id);
        await _decisionLogRepository.InsertAsync(transfer, autoSave: true);

        var expiring = NewDecision(
            rules,
            InventoryRuleTypes.ExpiringSoon,
            products["CT-001"].Id,
            malki.Id,
            DecisionTypes.ExpiryAlert,
            "في 6 قطع تارت فراولة بتنتهي صلاحيتها خلال يومين ولسه موجودة بواجهة الفرع.",
            "حطها ضمن عرض اليوم أو انقلها لفرع حركته أسرع.",
            stockAtEvaluation: 6);
        await _decisionLogRepository.InsertAsync(expiring, autoSave: true);

        var expired = NewDecision(
            rules,
            InventoryRuleTypes.ExpiredStock,
            products["BR-001"].Id,
            malki.Id,
            DecisionTypes.WasteWriteOff,
            "في 3 أرغفة باغيت انتهت صلاحيتها من يومين وما لازم تبقى ضمن المخزون القابل للبيع.",
            "اشطب الكمية المنتهية وسجّل سبب الهدر حتى يضل الجرد صحيح.",
            stockAtEvaluation: 3);
        await _decisionLogRepository.InsertAsync(expired, autoSave: true);

        var cashierVarianceRule = rules.Single(x => x.Id == IntelligenceConstants.CashierVarianceRuleId);
        var variance = new AppDecisionLog(
            _guidGenerator.Create(),
            cashierVarianceRule.Id,
            products["VN-001"].Id,
            mezza.Id,
            DecisionTypes.CashierVariance,
            "إغلاق وردية الصندوق طلع ناقص 1.50 دولار عن المبلغ المتوقع.",
            "راجع فواتير الوردية وعدّ الصندوق مرة ثانية قبل تثبيت التسوية.");
        variance.Acknowledge(admin.Id);
        await _decisionLogRepository.InsertAsync(variance, autoSave: true);

        var productionRule = rules.Single(x => x.Id == IntelligenceConstants.ProductionOperationsRuleId);
        var quality = new AppDecisionLog(
            _guidGenerator.Create(),
            productionRule.Id,
            products["CT-002"].Id,
            kitchen.Id,
            DecisionTypes.HighKitchenWaste,
            "نسبة هدر كيك الأوبرا ارتفعت بهالدفعة بسبب عدم تماسك أربع شرائح بعد التبريد.",
            "راجع حرارة التبريد وسماكة الطبقات قبل بدء الدفعة القادمة.");
        quality.Dismiss(admin.Id);
        await _decisionLogRepository.InsertAsync(quality, autoSave: true);
    }

    private AppDecisionLog NewDecision(
        List<AppInventoryRule> rules,
        string ruleType,
        Guid productId,
        Guid branchId,
        string decisionType,
        string reasoning,
        string suggestedAction,
        int? stockAtEvaluation = null,
        int? daysWithoutSale = null,
        Guid? sourceBranchId = null,
        Guid? targetBranchId = null)
    {
        var rule = rules
            .Where(x => x.IsActive && x.RuleType == ruleType)
            .OrderByDescending(x => x.Priority)
            .First();
        return new AppDecisionLog(
            _guidGenerator.Create(),
            rule.Id,
            productId,
            branchId,
            decisionType,
            reasoning,
            suggestedAction,
            stockAtEvaluation,
            daysWithoutSale,
            sourceBranchId,
            targetBranchId);
    }

    private static string StatusSummary(IEnumerable<string> statuses) =>
        string.Join(
            ", ",
            statuses
                .GroupBy(x => x)
                .OrderBy(x => x.Key)
                .Select(x => $"{x.Key}:{x.Count()}"));

    private sealed record PurchaseOrderSpec(
        string Status,
        string Notes,
        IReadOnlyDictionary<string, int> Quantities);

    private sealed record TransferSpec(
        int BranchIndex,
        string Sku,
        int Quantity,
        string Status,
        string Notes);
}
