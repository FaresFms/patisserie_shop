using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Inventory.Entities;
using Operations;
using Operations.PurchaseOrders;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Operations;

[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class PurchaseOrderExpiryTests : IntegrationTestBase
{
    private async Task<(IPurchaseOrderAppService Service, PurchaseOrderDto Order, PurchaseOrderItemDto Item)>
        ArrangeApprovedOrderAsync(int? shelfLifeDays)
    {
        var supplier = await CreateSupplierAsync();
        var category = await CreateCategoryAsync();
        var product = await CreateProductAsync(
            category.Id, supplier.Id, shelfLifeDays: shelfLifeDays);
        var branch = await CreateBranchAsync();
        var service = GetRequiredService<IPurchaseOrderAppService>();

        var order = await service.CreateAsync(new CreatePurchaseOrderDto
        {
            SupplierId = supplier.Id,
            DestBranchId = branch.Id,
            OrderDate = DateTime.UtcNow.Date,
            Currency = "USD"
        });
        var item = await service.AddItemAsync(order.Id, new AddPurchaseOrderItemDto
        {
            ProductId = product.Id,
            OrderedQuantity = 5,
            UnitPrice = product.CostPrice
        });
        await service.SubmitAsync(order.Id);
        order = await service.ApproveAsync(order.Id);
        return (service, order, item);
    }

    [Fact]
    public async Task Perishable_Receipt_Requires_An_Expiry_Date()
    {
        var (service, order, item) = await ArrangeApprovedOrderAsync(shelfLifeDays: 7);

        var exception = await Should.ThrowAsync<BusinessException>(() => service.ReceiveAsync(
            order.Id,
            new ReceiveItemsDto
            {
                Lines = new List<ReceiveLineDto>
                {
                    new() { ItemId = item.Id, ReceivedQuantity = 5 }
                }
            }));

        exception.Code.ShouldBe(OperationsErrorCodes.PurchaseExpiryRequired);
        (await service.GetAsync(order.Id)).Status.ShouldBe(PurchaseOrderStatuses.Approved);
    }

    [Fact]
    public async Task Perishable_Receipt_Creates_A_Batch_With_The_Entered_Expiry()
    {
        var (service, order, item) = await ArrangeApprovedOrderAsync(shelfLifeDays: 7);
        var expiry = DateTime.UtcNow.Date.AddDays(3);

        var received = await service.ReceiveAsync(order.Id, new ReceiveItemsDto
        {
            Lines = new List<ReceiveLineDto>
            {
                new() { ItemId = item.Id, ReceivedQuantity = 5, ExpiryDate = expiry }
            }
        });

        received.Status.ShouldBe(PurchaseOrderStatuses.Received);
        var batchRepository = GetRequiredService<IRepository<AppStockBatch, Guid>>();
        var batches = await WithUnitOfWorkAsync(() => batchRepository.GetListAsync(
            b => b.BranchId == order.DestBranchId && b.ProductId == item.ProductId));
        var batch = batches.Single();
        batch.ExpiryDate.ShouldBe(expiry);
        batch.QuantityRemaining.ShouldBe(5);
    }

    [Fact]
    public async Task Non_Perishable_Receipt_Does_Not_Require_An_Expiry_Date()
    {
        var (service, order, item) = await ArrangeApprovedOrderAsync(shelfLifeDays: null);

        var received = await service.ReceiveAsync(order.Id, new ReceiveItemsDto
        {
            Lines = new List<ReceiveLineDto>
            {
                new() { ItemId = item.Id, ReceivedQuantity = 5 }
            }
        });

        received.Status.ShouldBe(PurchaseOrderStatuses.Received);
    }
}
