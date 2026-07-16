using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory;
using Inventory.Branches;
using Inventory.Products;
using Operations.StockTransfers;
using patisserie_shop.EntityFrameworkCore.Testing;
using Production;
using Production.BranchRequests;
using Production.Dispatch;
using Production.Formulas;
using Production.Orders;
using Production.Plans;
using Shouldly;
using Volo.Abp;
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
            BranchType = BranchTypes.SalesBranch
        });

        await InitializeInventoryAsync(kitchen.Id, rawMaterial.Id, 100);
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
        plan.Lines.Single(x => x.ProductId == finishedProduct.Id).PlannedQuantity.ShouldBe(10);
        await planService.ConfirmAsync(plan.Id);
        var cancellationException = await Should.ThrowAsync<BusinessException>(
            () => planService.CancelAsync(plan.Id));
        cancellationException.Code.ShouldBe(ProductionErrorCodes.CannotCancelPlanWithOrders);

        request = await requestService.GetAsync(request.Id);
        request.Items.Single().PlannedQuantity.ShouldBe(10);
        request.Items.Single().FulfilledQuantity.ShouldBe(0);
        request.Status.ShouldBe(BranchProductionRequestStatuses.Planned);

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
        order.ReservedQuantity.ShouldBe(10);

        await orderService.StartAsync(order.Id);
        (await planService.GetAsync(plan.Id)).Status.ShouldBe(ProductionPlanStatuses.InProgress);
        order = await orderService.CompleteAsync(order.Id, new CompleteProductionOrderDto
        {
            ActualOutputQuantity = 10,
            AcceptedQuantity = 10,
            RejectedQuantity = 0,
            ExpiryDate = DateTime.UtcNow.Date.AddDays(3)
        });
        order.RemainingToDispatch.ShouldBe(10);
        (await planService.GetAsync(plan.Id)).Status.ShouldBe(ProductionPlanStatuses.Closed);

        var dispatchService = GetRequiredService<IProductionDispatchAppService>();
        var firstDispatch = await dispatchService.CreateTransferAsync(
            new CreateProductionDispatchTransferDto
            {
                ProductionOrderId = order.Id,
                DestinationBranchId = salesBranch.Id,
                Quantity = 4
            });
        var secondDispatch = await dispatchService.CreateTransferAsync(
            new CreateProductionDispatchTransferDto
            {
                ProductionOrderId = order.Id,
                DestinationBranchId = salesBranch.Id,
                Quantity = 6
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
        request.Items.Single().FulfilledQuantity.ShouldBe(0);
        request.Status.ShouldBe(BranchProductionRequestStatuses.Planned);

        var transferService = GetRequiredService<IStockTransferAppService>();
        var firstTransfer = await transferService.GetAsync(firstDispatch.StockTransferId);
        await transferService.CompleteAsync(firstTransfer.Id, new CompleteStockTransferDto
        {
            Lines = new List<CompleteTransferLineDto>
            {
                new() { ItemId = firstTransfer.Items.Single().Id, TransferredQuantity = 4 }
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
        request.Items.Single().FulfilledQuantity.ShouldBe(7);
        request.Items.Single().PlannedQuantity.ShouldBe(7);
        request.Status.ShouldBe(BranchProductionRequestStatuses.PartiallyFulfilled);

        var recoveryPlan = await planService.CreateDraftAsync(new CreateProductionPlanDto
        {
            KitchenBranchId = kitchen.Id,
            ProductionDate = DateTime.UtcNow.Date
        });
        recoveryPlan.Lines.Single(x => x.ProductId == finishedProduct.Id)
            .RequestedQuantity.ShouldBe(3);
    }
}
