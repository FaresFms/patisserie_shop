using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Operations.Entities;
using Operations.PurchaseOrders;
using Operations.Sales;
using Operations.StockTransfers;
using patisserie_shop.EntityFrameworkCore.Testing;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Xunit;

namespace patisserie_shop.EntityFrameworkCore.Operations;

[Collection(patisserie_shopTestConsts.CollectionDefinitionName)]
public class BranchScopeTests : IntegrationTestBase
{
    [Fact]
    public async Task Purchase_order_list_is_limited_to_visible_destination_branches()
    {
        var supplier = await CreateSupplierAsync();
        var branchA = await CreateBranchAsync();
        var branchB = await CreateBranchAsync();
        var repository = GetRequiredService<IPurchaseOrderRepository>();
        var guidGenerator = GetRequiredService<IGuidGenerator>();

        await WithUnitOfWorkAsync(async () =>
        {
            await repository.InsertAsync(new AppPurchaseOrder(
                guidGenerator.Create(), supplier.Id, branchA.Id, "PO-SCOPE-A", DateTime.UtcNow.Date));
            await repository.InsertAsync(new AppPurchaseOrder(
                guidGenerator.Create(), supplier.Id, branchB.Id, "PO-SCOPE-B", DateTime.UtcNow.Date));
        });

        var rows = await WithUnitOfWorkAsync(() => repository.GetFilteredListAsync(
            filter: null,
            status: null,
            supplierId: null,
            destBranchId: null,
            fromDate: null,
            toDate: null,
            visibleBranchIds: new List<Guid> { branchA.Id },
            sorting: string.Empty,
            skipCount: 0,
            maxResultCount: 10));

        rows.Select(x => x.DestBranchId).ShouldBe(new[] { branchA.Id });
    }

    [Fact]
    public async Task Stock_transfer_list_for_branch_manager_shows_only_managed_source_or_destination()
    {
        var branchA = await CreateBranchAsync();
        var branchB = await CreateBranchAsync();
        var branchC = await CreateBranchAsync();
        var repository = GetRequiredService<IStockTransferRepository>();
        var guidGenerator = GetRequiredService<IGuidGenerator>();

        await WithUnitOfWorkAsync(async () =>
        {
            await repository.InsertAsync(new AppStockTransfer(
                guidGenerator.Create(), branchA.Id, branchB.Id, DateTime.UtcNow.Date));
            await repository.InsertAsync(new AppStockTransfer(
                guidGenerator.Create(), branchB.Id, branchA.Id, DateTime.UtcNow.Date));
            await repository.InsertAsync(new AppStockTransfer(
                guidGenerator.Create(), branchB.Id, branchC.Id, DateTime.UtcNow.Date));
        });

        var visibleToBranchA = await WithUnitOfWorkAsync(() => repository.GetFilteredListAsync(
            status: null,
            fromBranchId: null,
            toBranchId: null,
            filter: null,
            sorting: string.Empty,
            skipCount: 0,
            maxResultCount: 10,
            visibility: new StockTransferVisibilitySpec
            {
                CanViewAll = false,
                ManagedBranchIds = new List<Guid> { branchA.Id }
            }));

        visibleToBranchA.Count.ShouldBe(2);
        visibleToBranchA.ShouldAllBe(row =>
            row.Transfer.FromBranchId == branchA.Id || row.Transfer.ToBranchId == branchA.Id);

        var visibleToAll = await WithUnitOfWorkAsync(() => repository.GetFilteredListAsync(
            status: null,
            fromBranchId: null,
            toBranchId: null,
            filter: null,
            sorting: string.Empty,
            skipCount: 0,
            maxResultCount: 10,
            visibility: new StockTransferVisibilitySpec { CanViewAll = true }));

        visibleToAll.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Generated_operation_numbers_use_unique_concurrency_resistant_suffixes()
    {
        var poManager = GetRequiredService<PurchaseOrderManager>();
        var saleManager = GetRequiredService<SaleManager>();

        var poNumbers = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => poManager.GenerateNumberAsync(2026)));
        var invoiceNumbers = await Task.WhenAll(Enumerable.Range(0, 50)
            .Select(_ => saleManager.GenerateInvoiceNumberAsync(2026)));

        poNumbers.Distinct().Count().ShouldBe(poNumbers.Length);
        invoiceNumbers.Distinct().Count().ShouldBe(invoiceNumbers.Length);
        poNumbers.ShouldAllBe(number => number.StartsWith("PO-2026-") && number.Length == 16);
        invoiceNumbers.ShouldAllBe(number => number.StartsWith("INV-2026-") && number.Length == 17);
    }
}
