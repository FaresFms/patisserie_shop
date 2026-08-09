using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Intelligence.Entities;
using Inventory;
using Inventory.BranchInventory;
using Inventory.Branches;
using Inventory.Entities;
using Inventory.Products;
using Inventory.StockBatches;
using Operations.StockTransfers;
using patisserie_shop.EntityFrameworkCore.Testing;
using Production;
using Production.Analytics;
using Production.BranchRequests;
using Production.Dispatch;
using Production.Dashboard;
using Production.Formulas;
using Production.Orders;
using Production.Plans;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Users;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Production;

[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class ProductionFlowTests : IntegrationTestBase
{
    [Fact]
    public async Task Request_is_planned_once_and_is_fulfilled_only_by_actual_receipt()
    {
        var category = await CreateCategoryAsync();
        var productService = GetRequiredService<IProductAppService>();
        var branchService = GetRequiredService<IBranchAppService>();
        var currentUserId = GetRequiredService<ICurrentUser>().GetId();

        var rawMaterial = await productService.CreateAsync(new CreateProductDto
        {
            CategoryId = category.Id,
            Name = NextName("Flour"),
            SKU = NextName("RAW"),
            Unit = "g",
            CostPrice = 1m,
            SalePrice = 1m,
            ProductType = ProductTypes.RawMaterial,
            IsSellable = false,
            IsPurchasable = true,
            IsProducible = false
        });
        var finishedProduct = await productService.CreateAsync(new CreateProductDto
        {
            CategoryId = category.Id,
            Name = NextName("Cake"),
            SKU = NextName("FIN"),
            Unit = "pcs",
            CostPrice = 2m,
            SalePrice = 5m,
            ProductType = ProductTypes.FinishedGood,
            IsSellable = true,
            IsPurchasable = false,
            IsProducible = true,
            ShelfLifeDays = 3
        });
        var kitchen = await branchService.CreateAsync(new CreateBranchDto
        {
            Name = NextName("Main kitchen"),
            BranchType = BranchTypes.MainKitchen
        });
        var salesBranch = await branchService.CreateAsync(new CreateBranchDto
        {
            Name = NextName("Sales branch"),
            BranchType = BranchTypes.SalesBranch,
            ManagerUserId = currentUserId
        });
        var otherSalesBranch = await branchService.CreateAsync(new CreateBranchDto
        {
            Name = NextName("Other sales branch"),
            BranchType = BranchTypes.SalesBranch
        });

        await InitializeInventoryAsync(kitchen.Id, rawMaterial.Id, 100);
        var finishedInventoryDto = await InitializeInventoryAsync(kitchen.Id, finishedProduct.Id, 0);
        await WithUnitOfWorkAsync(async () =>
        {
            var inventoryRepository = GetRequiredService<IRepository<AppBranchInventory, Guid>>();
            var inventoryManager = GetRequiredService<BranchInventoryManager>();
            var finishedInventory = await inventoryRepository.GetAsync(finishedInventoryDto.Id);
            await inventoryManager.AdjustStockAsync(
                finishedInventory,
                newQuantity: 4,
                StockMovementTypes.ProductionOutput,
                notes: "existing usable kitchen stock",
                batchExpiryDate: DateTime.UtcNow.Date.AddDays(3));
            await inventoryRepository.UpdateAsync(finishedInventory);
        });
        await GetRequiredService<IProductionFormulaAppService>().CreateAsync(new CreateProductionFormulaDto
        {
            FinishedProductId = finishedProduct.Id,
            FormulaName = NextName("Cake formula"),
            OutputQuantity = 10,
            IsActive = true,
            IsDefault = true,
            Items = new List<CreateProductionFormulaItemDto>
            {
                new() { IngredientProductId = rawMaterial.Id, Quantity = 20 }
            }
        });

        var requestService = GetRequiredService<IBranchProductionRequestAppService>();
        var requestableBranches = await requestService.GetRequestableBranchesLookupAsync();
        requestableBranches.Select(branch => branch.Id).ShouldContain(salesBranch.Id);
        requestableBranches.Select(branch => branch.Id).ShouldNotContain(otherSalesBranch.Id);
        requestableBranches.Count.ShouldBe(1);
        var request = await requestService.CreateAsync(new CreateBranchProductionRequestDto
        {
            BranchId = salesBranch.Id,
            NeededByDate = DateTime.UtcNow.Date,
            Items = new List<CreateBranchProductionRequestItemDto>
            {
                new() { ProductId = finishedProduct.Id, RequestedQuantity = 10 }
            }
        });
        await requestService.SubmitAsync(request.Id);
        request = await requestService.ApproveAsync(request.Id, new ApproveBranchProductionRequestDto
        {
            Items = new List<ApproveBranchProductionRequestItemDto>
            {
                new() { ItemId = request.Items.Single().Id, ApprovedQuantity = 10 }
            }
        });

        var planService = GetRequiredService<IProductionPlanAppService>();
        var plan = await planService.CreateDraftAsync(new CreateProductionPlanDto
        {
            KitchenBranchId = kitchen.Id,
            ProductionDate = DateTime.UtcNow.Date
        });
        var plannedLine = plan.Lines.Single(x => x.ProductId == finishedProduct.Id);
        plannedLine.CurrentKitchenStock.ShouldBe(4);
        plannedLine.PlannedQuantity.ShouldBe(6);
        await planService.ConfirmAsync(plan.Id);
        var cancellationException = await Should.ThrowAsync<BusinessException>(
            () => planService.CancelAsync(plan.Id));
        cancellationException.Code.ShouldBe(ProductionErrorCodes.CannotCancelPlanWithOrders);

        request = await requestService.GetAsync(request.Id);
        request.Items.Single().PlannedQuantity.ShouldBe(6);
        request.Items.Single().FulfilledQuantity.ShouldBe(0);
        request.Status.ShouldBe(BranchProductionRequestStatuses.PartiallyPlanned);

        var dispatchService = GetRequiredService<IProductionDispatchAppService>();
        var stockQueue = await dispatchService.GetStockDispatchQueueAsync(
            new GetProductionStockDispatchQueueInput { KitchenBranchId = kitchen.Id });
        var stockTarget = stockQueue.ShouldHaveSingleItem();
        stockTarget.RemainingUnplannedQuantity.ShouldBe(4);
        stockTarget.DispatchableQuantity.ShouldBe(4);

        var stockDispatch = await dispatchService.CreateRequestStockTransferAsync(
            new CreateProductionRequestStockTransferDto
            {
                KitchenBranchId = kitchen.Id,
                RequestId = request.Id,
                RequestItemId = request.Items.Single().Id,
                Quantity = 4
            });
        request = await requestService.GetAsync(request.Id);
        request.Items.Single().PlannedQuantity.ShouldBe(10);
        request.Items.Single().FulfilledQuantity.ShouldBe(0);

        var transferService = GetRequiredService<IStockTransferAppService>();
        var stockTransfer = await transferService.GetAsync(stockDispatch.StockTransferId);
        await transferService.CompleteAsync(stockTransfer.Id, new CompleteStockTransferDto
        {
            Lines = new List<CompleteTransferLineDto>
            {
                new() { ItemId = stockTransfer.Items.Single().Id, TransferredQuantity = 4 }
            }
        });
        request = await requestService.GetAsync(request.Id);
        request.Items.Single().FulfilledQuantity.ShouldBe(4);
        request.Items.Single().PlannedQuantity.ShouldBe(10);
        request.Status.ShouldBe(BranchProductionRequestStatuses.PartiallyFulfilled);
        var receivedStockBatches = await GetRequiredService<IStockBatchRepository>()
            .GetOpenBatchesAsync(salesBranch.Id, finishedProduct.Id);
        receivedStockBatches.Sum(batch => batch.QuantityRemaining).ShouldBe(4);
        receivedStockBatches.ShouldAllBe(batch => batch.UnitCost == 2m);

        var secondPlan = await planService.CreateDraftAsync(new CreateProductionPlanDto
        {
            KitchenBranchId = kitchen.Id,
            ProductionDate = DateTime.UtcNow.Date
        });
        secondPlan.Lines.Any(x => x.ProductId == finishedProduct.Id).ShouldBeFalse();

        var orderService = GetRequiredService<IProductionOrderAppService>();
        var orders = await orderService.GetListAsync(new GetProductionOrdersInput
        {
            KitchenBranchId = kitchen.Id,
            MaxResultCount = 20
        });
        var order = await orderService.GetAsync(orders.Items.Single().Id);
        order.ReservedQuantity.ShouldBe(6);

        var scheduledStart = DateTime.UtcNow.AddHours(1);
        await orderService.ScheduleAsync(order.Id, new ScheduleProductionOrderDto
        {
            WorkCenterCode = "MAIN",
            ShiftCode = "DAY",
            ScheduledStartTime = scheduledStart,
            ScheduledEndTime = scheduledStart.AddHours(1),
            OperatorUserId = currentUserId,
            OperatorName = "Integration Test Baker"
        });
        await orderService.StartAsync(order.Id);
        (await planService.GetAsync(plan.Id)).Status.ShouldBe(ProductionPlanStatuses.InProgress);
        order = await orderService.CompleteAsync(order.Id, new CompleteProductionOrderDto
        {
            ActualOutputQuantity = 6,
            AcceptedQuantity = 6,
            RejectedQuantity = 0,
            ExpiryDate = DateTime.UtcNow.Date.AddDays(3)
        });
        order.RemainingToDispatch.ShouldBe(6);
        (await planService.GetAsync(plan.Id)).Status.ShouldBe(ProductionPlanStatuses.Closed);
        order = await orderService.ReleaseQualityAsync(order.Id, new ProductionQualityActionDto
        {
            Reason = "Integration quality check passed"
        });
        order.QualityStatus.ShouldBe(ProductionQualityStatuses.Released);

        var firstDispatch = await dispatchService.CreateTransferAsync(
            new CreateProductionDispatchTransferDto
            {
                ProductionOrderId = order.Id,
                DestinationBranchId = salesBranch.Id,
                Quantity = 2
            });
        var secondDispatch = await dispatchService.CreateTransferAsync(
            new CreateProductionDispatchTransferDto
            {
                ProductionOrderId = order.Id,
                DestinationBranchId = salesBranch.Id,
                Quantity = 4
            });

        var dispatchException = await Should.ThrowAsync<BusinessException>(() =>
            dispatchService.CreateTransferAsync(new CreateProductionDispatchTransferDto
            {
                ProductionOrderId = order.Id,
                DestinationBranchId = salesBranch.Id,
                Quantity = 1
            }));
        dispatchException.Code.ShouldBe(ProductionErrorCodes.DispatchQuantityExceedsRemaining);

        request = await requestService.GetAsync(request.Id);
        request.Items.Single().FulfilledQuantity.ShouldBe(4);
        request.Status.ShouldBe(BranchProductionRequestStatuses.PartiallyFulfilled);

        var firstTransfer = await transferService.GetAsync(firstDispatch.StockTransferId);
        await transferService.CompleteAsync(firstTransfer.Id, new CompleteStockTransferDto
        {
            Lines = new List<CompleteTransferLineDto>
            {
                new() { ItemId = firstTransfer.Items.Single().Id, TransferredQuantity = 2 }
            }
        });
        var secondTransfer = await transferService.GetAsync(secondDispatch.StockTransferId);
        await transferService.CompleteAsync(secondTransfer.Id, new CompleteStockTransferDto
        {
            Lines = new List<CompleteTransferLineDto>
            {
                new() { ItemId = secondTransfer.Items.Single().Id, TransferredQuantity = 3 }
            }
        });

        request = await requestService.GetAsync(request.Id);
        request.Items.Single().FulfilledQuantity.ShouldBe(9);
        request.Items.Single().PlannedQuantity.ShouldBe(9);
        request.Status.ShouldBe(BranchProductionRequestStatuses.PartiallyFulfilled);

        var recoveryPlan = await planService.CreateDraftAsync(new CreateProductionPlanDto
        {
            KitchenBranchId = kitchen.Id,
            ProductionDate = DateTime.UtcNow.Date
        });
        recoveryPlan.Lines.Single(x => x.ProductId == finishedProduct.Id)
            .RequestedQuantity.ShouldBe(1);

        await planService.ConfirmAsync(recoveryPlan.Id);
        var refreshedOrders = await orderService.GetListAsync(new GetProductionOrdersInput
        {
            KitchenBranchId = kitchen.Id,
            MaxResultCount = 20
        });
        var recoveryOrder = await orderService.GetAsync(
            refreshedOrders.Items.Single(x => x.Id != order.Id).Id);
        var recoveryStart = DateTime.UtcNow.AddHours(2);
        await orderService.ScheduleAsync(recoveryOrder.Id, new ScheduleProductionOrderDto
        {
            WorkCenterCode = "MAIN",
            ShiftCode = "DAY",
            ScheduledStartTime = recoveryStart,
            ScheduledEndTime = recoveryStart.AddHours(1),
            OperatorUserId = currentUserId,
            OperatorName = "Integration Test Baker"
        });
        await orderService.StartAsync(recoveryOrder.Id);
        recoveryOrder = await orderService.CompleteAsync(recoveryOrder.Id, new CompleteProductionOrderDto
        {
            ActualOutputQuantity = 1,
            AcceptedQuantity = 1,
            RejectedQuantity = 0,
            ExpiryDate = DateTime.UtcNow.Date.AddDays(3)
        });
        recoveryOrder = await orderService.ReleaseQualityAsync(recoveryOrder.Id, new ProductionQualityActionDto
        {
            Reason = "Integration quality check passed"
        });
        var recoveryDispatch = await dispatchService.CreateTransferAsync(
            new CreateProductionDispatchTransferDto
            {
                ProductionOrderId = recoveryOrder.Id,
                DestinationBranchId = salesBranch.Id,
                Quantity = 1
            });
        var recoveryTransfer = await transferService.GetAsync(recoveryDispatch.StockTransferId);
        await transferService.CompleteAsync(recoveryTransfer.Id, new CompleteStockTransferDto
        {
            Lines = new List<CompleteTransferLineDto>
            {
                new() { ItemId = recoveryTransfer.Items.Single().Id, TransferredQuantity = 1 }
            }
        });

        request = await requestService.GetForReviewAsync(request.Id);
        request.Status.ShouldBe(BranchProductionRequestStatuses.Fulfilled);
        var decisionRepository = GetRequiredService<IRepository<AppDecisionLog, Guid>>();
        var decisionsBeforeReports = await decisionRepository.GetCountAsync();
        var analytics = await GetRequiredService<IProductionAnalyticsAppService>()
            .GetAsync(new GetProductionAnalyticsInput { Days = 30, KitchenBranchId = kitchen.Id });
        await GetRequiredService<IProductionDashboardAppService>()
            .GetAsync(new GetProductionDashboardInput { KitchenBranchId = kitchen.Id });
        analytics.ApprovedRequestQuantity.ShouldBe(10);
        analytics.FulfilledRequestQuantity.ShouldBe(10);
        analytics.FulfillmentPercent.ShouldBe(100m);
        (await decisionRepository.GetCountAsync()).ShouldBe(decisionsBeforeReports);
    }

}
