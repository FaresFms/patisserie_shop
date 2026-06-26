using System;
using Production;
using Production.Entities;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace patisserie_shop.Production;

public class AppProductionWasteTests
{
    [Fact]
    public void Constructor_records_costed_waste()
    {
        var orderId = Guid.NewGuid();
        var branchId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var recordedAt = DateTime.UtcNow;

        var waste = new AppProductionWaste(
            Guid.NewGuid(),
            orderId,
            branchId,
            productId,
            ProductionWasteTypes.RejectedOutput,
            quantity: 3,
            unitCost: 2.75m,
            ProductionWasteReasons.Burned,
            recordedByUserId: Guid.NewGuid(),
            recordedAt,
            notes: "edge tray");

        waste.ProductionOrderId.ShouldBe(orderId);
        waste.KitchenBranchId.ShouldBe(branchId);
        waste.ProductId.ShouldBe(productId);
        waste.WasteType.ShouldBe(ProductionWasteTypes.RejectedOutput);
        waste.Quantity.ShouldBe(3);
        waste.UnitCost.ShouldBe(2.75m);
        waste.TotalCost.ShouldBe(8.25m);
        waste.Reason.ShouldBe(ProductionWasteReasons.Burned);
        waste.RecordedAt.ShouldBe(recordedAt);
    }

    [Fact]
    public void Constructor_rejects_invalid_quantity_and_cost()
    {
        Should.Throw<BusinessException>(() => NewWaste(quantity: 0))
            .Code.ShouldBe(ProductionErrorCodes.InvalidWasteQuantity);

        Should.Throw<BusinessException>(() => NewWaste(unitCost: -1m))
            .Code.ShouldBe(ProductionErrorCodes.InvalidWasteCost);
    }

    [Fact]
    public void Constructor_rejects_unknown_type_and_reason()
    {
        Should.Throw<BusinessException>(() => NewWaste(wasteType: "Mystery"))
            .Code.ShouldBe(ProductionErrorCodes.InvalidWasteType);

        Should.Throw<BusinessException>(() => NewWaste(reason: "Mystery"))
            .Code.ShouldBe(ProductionErrorCodes.InvalidWasteReason);
    }

    private static AppProductionWaste NewWaste(
        string wasteType = ProductionWasteTypes.ManualWriteOff,
        int quantity = 1,
        decimal unitCost = 1m,
        string reason = ProductionWasteReasons.Other)
    {
        return new AppProductionWaste(
            Guid.NewGuid(),
            productionOrderId: null,
            kitchenBranchId: Guid.NewGuid(),
            productId: Guid.NewGuid(),
            wasteType,
            quantity,
            unitCost,
            reason,
            recordedByUserId: null,
            recordedAt: DateTime.UtcNow,
            notes: null);
    }
}
