using System;
using System.Collections.Generic;
using System.Linq;
using Operations;
using Operations.Entities;
using Operations.Events;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Operations;

/// <summary>
/// Pure unit tests for the AppSale aggregate: line invariants, the stock-snapshot
/// validation in Record(), and the SaleRecordedEto raised from the aggregate.
/// </summary>
public class AppSaleTests
{
    private static AppSale NewSale() => new(
        Guid.NewGuid(),
        branchId: Guid.NewGuid(),
        invoiceNumber: "INV-2026-0001",
        saleDate: new DateTime(2026, 6, 1));

    [Fact]
    public void AddItem_Should_Reject_Duplicate_Product()
    {
        var sale = NewSale();
        var productId = Guid.NewGuid();
        sale.AddItem(Guid.NewGuid(), productId, quantity: 1, unitPrice: 10m);

        var ex = Should.Throw<BusinessException>(() =>
            sale.AddItem(Guid.NewGuid(), productId, quantity: 2, unitPrice: 10m));

        ex.Code.ShouldBe(OperationsErrorCodes.DuplicateProductInSale);
        sale.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void AddItem_Should_Reject_NonPositive_Quantity_And_Negative_Price()
    {
        var sale = NewSale();

        Should.Throw<BusinessException>(() => sale.AddItem(Guid.NewGuid(), Guid.NewGuid(), 0, 10m))
            .Code.ShouldBe(OperationsErrorCodes.InvalidQuantity);
        Should.Throw<BusinessException>(() => sale.AddItem(Guid.NewGuid(), Guid.NewGuid(), -1, 10m))
            .Code.ShouldBe(OperationsErrorCodes.InvalidQuantity);
        Should.Throw<BusinessException>(() => sale.AddItem(Guid.NewGuid(), Guid.NewGuid(), 1, -0.01m))
            .Code.ShouldBe(OperationsErrorCodes.InvalidPrice);

        sale.Items.ShouldBeEmpty();
        sale.TotalAmount.ShouldBe(0m);
    }

    [Fact]
    public void AddItem_Should_Recalculate_Total_From_Line_Subtotals()
    {
        var sale = NewSale();
        sale.TotalAmount.ShouldBe(0m);

        sale.AddItem(Guid.NewGuid(), Guid.NewGuid(), quantity: 3, unitPrice: 2.50m);
        sale.TotalAmount.ShouldBe(7.50m);

        sale.AddItem(Guid.NewGuid(), Guid.NewGuid(), quantity: 2, unitPrice: 4.00m);
        sale.TotalAmount.ShouldBe(15.50m);
        sale.Items.Sum(i => i.Subtotal).ShouldBe(sale.TotalAmount);
    }

    [Fact]
    public void Record_Should_Throw_On_Empty_Sale()
    {
        var sale = NewSale();

        var ex = Should.Throw<BusinessException>(() =>
            sale.Record(new Dictionary<Guid, int>()));

        ex.Code.ShouldBe(OperationsErrorCodes.CannotRecordEmptySale);
    }

    [Fact]
    public void Record_Should_Throw_When_Inventory_Row_Is_Missing()
    {
        var sale = NewSale();
        sale.AddItem(Guid.NewGuid(), Guid.NewGuid(), quantity: 1, unitPrice: 5m);

        // Snapshot has no entry for the sold product → branch never initialized it.
        var ex = Should.Throw<BusinessException>(() =>
            sale.Record(new Dictionary<Guid, int>()));

        ex.Code.ShouldBe(OperationsErrorCodes.NoInventoryRow);
    }

    [Fact]
    public void Record_Should_Throw_When_Stock_Is_Insufficient()
    {
        var sale = NewSale();
        var productId = Guid.NewGuid();
        sale.AddItem(Guid.NewGuid(), productId, quantity: 5, unitPrice: 5m);

        var ex = Should.Throw<BusinessException>(() =>
            sale.Record(new Dictionary<Guid, int> { [productId] = 4 }));

        ex.Code.ShouldBe(OperationsErrorCodes.InsufficientStock);
    }

    [Fact]
    public void Record_Should_Publish_SaleRecordedEto_When_Stock_Covers_All_Lines()
    {
        var sale = NewSale();
        var productId = Guid.NewGuid();
        sale.AddItem(Guid.NewGuid(), productId, quantity: 5, unitPrice: 5m);

        sale.Record(new Dictionary<Guid, int> { [productId] = 5 }); // exactly enough

        var eto = sale.GetDistributedEvents()
            .Select(e => e.EventData)
            .OfType<SaleRecordedEto>()
            .ShouldHaveSingleItem();
        eto.SaleId.ShouldBe(sale.Id);
        eto.BranchId.ShouldBe(sale.BranchId);
    }

    // ─── Cashier POS additions: shift link + void state ───

    [Fact]
    public void AssignShift_Should_Link_The_Sale_To_A_Shift()
    {
        var sale = NewSale();
        sale.ShiftId.ShouldBeNull();

        var shiftId = Guid.NewGuid();
        sale.AssignShift(shiftId);

        sale.ShiftId.ShouldBe(shiftId);
    }

    [Fact]
    public void Cash_Sale_Requires_The_Full_Tendered_Amount()
    {
        var sale = NewSale();
        sale.AddItem(Guid.NewGuid(), Guid.NewGuid(), quantity: 2, unitPrice: 5m);

        Should.Throw<BusinessException>(() => sale.EnsureCashTendered(null))
            .Code.ShouldBe(OperationsErrorCodes.CashTenderedInsufficient);
        Should.Throw<BusinessException>(() => sale.EnsureCashTendered(9.99m))
            .Code.ShouldBe(OperationsErrorCodes.CashTenderedInsufficient);

        Should.NotThrow(() => sale.EnsureCashTendered(10m));
        Should.NotThrow(() => sale.EnsureCashTendered(20m));
    }

    [Fact]
    public void Void_Should_Flip_State_And_Capture_Who_When_And_Why()
    {
        var sale = NewSale();
        sale.IsVoided.ShouldBeFalse();

        var userId = Guid.NewGuid();
        var when = new DateTime(2026, 6, 18, 12, 30, 0, DateTimeKind.Utc);
        sale.Void(userId, "Customer changed mind", when);

        sale.IsVoided.ShouldBeTrue();
        sale.VoidedByUserId.ShouldBe(userId);
        sale.VoidReason.ShouldBe("Customer changed mind");
        sale.VoidedAt.ShouldBe(when);
    }

    [Fact]
    public void Void_Should_Throw_When_Already_Voided()
    {
        var sale = NewSale();
        sale.Void(Guid.NewGuid(), reason: null, DateTime.UtcNow);

        Should.Throw<BusinessException>(() => sale.Void(Guid.NewGuid(), reason: null, DateTime.UtcNow))
            .Code.ShouldBe(OperationsErrorCodes.SaleAlreadyVoided);
    }
}
