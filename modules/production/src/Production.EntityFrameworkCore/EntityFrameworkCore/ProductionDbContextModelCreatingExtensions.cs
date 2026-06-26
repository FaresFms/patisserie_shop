using Microsoft.EntityFrameworkCore;
using Production.Entities;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Production.EntityFrameworkCore;

public static class ProductionDbContextModelCreatingExtensions
{
    public static void ConfigureProduction(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<AppProductionFormula>(b =>
        {
            b.ToTable(ProductionDbProperties.DbTablePrefix + "Formulas", ProductionDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.FormulaName).IsRequired().HasMaxLength(128);
            b.Property(x => x.Notes).HasMaxLength(1024);

            // Money: 18,2. Percent: 5,2. No HasDefaultValue — defaults live in the entity.
            b.Property(x => x.ExpectedWastePercent).HasPrecision(5, 2);
            b.Property(x => x.LaborCostPerBatch).HasPrecision(18, 2);
            b.Property(x => x.OverheadCostPerBatch).HasPrecision(18, 2);

            b.HasIndex(x => x.FinishedProductId).HasDatabaseName("IX_ProductionFormulas_FinishedProduct");

            // Owned children — accessed only through the root (no child repository).
            b.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(i => i.FormulaId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<AppProductionFormulaItem>(b =>
        {
            b.ToTable(ProductionDbProperties.DbTablePrefix + "FormulaItems", ProductionDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.LossPercent).HasPrecision(5, 2);

            b.HasIndex(x => x.FormulaId).HasDatabaseName("IX_ProductionFormulaItems_Formula");
        });

        builder.Entity<AppBranchProductionRequest>(b =>
        {
            b.ToTable(ProductionDbProperties.DbTablePrefix + "BranchRequests", ProductionDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.RequestNumber).IsRequired().HasMaxLength(32);
            b.Property(x => x.Priority).IsRequired().HasMaxLength(16);
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);
            b.Property(x => x.Notes).HasMaxLength(1024);
            b.Property(x => x.DecisionReason).HasMaxLength(1024);
            b.Property(x => x.RejectionReason).HasMaxLength(1024);

            b.HasIndex(x => x.RequestNumber).IsUnique().HasDatabaseName("IX_ProductionBranchRequests_Number");
            b.HasIndex(x => new { x.BranchId, x.Status, x.NeededByDate }).HasDatabaseName("IX_ProductionBranchRequests_BranchStatusDue");

            b.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(i => i.RequestId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<AppBranchProductionRequestItem>(b =>
        {
            b.ToTable(ProductionDbProperties.DbTablePrefix + "BranchRequestItems", ProductionDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.Notes).HasMaxLength(512);
            b.HasIndex(x => x.RequestId).HasDatabaseName("IX_ProductionBranchRequestItems_Request");
            b.HasIndex(x => x.ProductId).HasDatabaseName("IX_ProductionBranchRequestItems_Product");
        });

        builder.Entity<AppProductionPlan>(b =>
        {
            b.ToTable(ProductionDbProperties.DbTablePrefix + "Plans", ProductionDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.PlanNumber).IsRequired().HasMaxLength(32);
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);
            b.Property(x => x.Notes).HasMaxLength(1024);

            b.HasIndex(x => x.PlanNumber).IsUnique().HasDatabaseName("IX_ProductionPlans_Number");
            b.HasIndex(x => new { x.KitchenBranchId, x.ProductionDate, x.Status }).HasDatabaseName("IX_ProductionPlans_KitchenDateStatus");

            b.HasMany(x => x.Lines)
                .WithOne()
                .HasForeignKey(i => i.PlanId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Lines).HasField("_lines").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<AppProductionPlanLine>(b =>
        {
            b.ToTable(ProductionDbProperties.DbTablePrefix + "PlanLines", ProductionDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.OverrideReason).HasMaxLength(1024);
            b.Property(x => x.EstimatedIngredientCost).HasPrecision(18, 2);
            b.Property(x => x.EstimatedLaborCost).HasPrecision(18, 2);
            b.Property(x => x.EstimatedOverheadCost).HasPrecision(18, 2);
            b.Property(x => x.EstimatedTotalCost).HasPrecision(18, 2);

            b.HasIndex(x => x.PlanId).HasDatabaseName("IX_ProductionPlanLines_Plan");
            b.HasIndex(x => x.ProductId).HasDatabaseName("IX_ProductionPlanLines_Product");
        });

        builder.Entity<AppProductionOrder>(b =>
        {
            b.ToTable(ProductionDbProperties.DbTablePrefix + "Orders", ProductionDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.OrderNumber).IsRequired().HasMaxLength(32);
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);
            b.Property(x => x.Priority).IsRequired().HasMaxLength(16);
            b.Property(x => x.WasteReason).HasMaxLength(512);
            b.Property(x => x.Notes).HasMaxLength(1024);

            b.Property(x => x.PlannedIngredientCost).HasPrecision(18, 2);
            b.Property(x => x.ActualIngredientCost).HasPrecision(18, 2);
            b.Property(x => x.LaborCost).HasPrecision(18, 2);
            b.Property(x => x.OverheadCost).HasPrecision(18, 2);
            b.Property(x => x.TotalProductionCost).HasPrecision(18, 2);
            b.Property(x => x.UnitProductionCost).HasPrecision(18, 4);

            b.HasIndex(x => x.OrderNumber).IsUnique().HasDatabaseName("IX_ProductionOrders_Number");
            b.HasIndex(x => new { x.KitchenBranchId, x.Status }).HasDatabaseName("IX_ProductionOrders_KitchenStatus");
            b.HasIndex(x => x.ProductionPlanLineId).HasDatabaseName("IX_ProductionOrders_PlanLine");
            b.HasIndex(x => x.FinishedProductId).HasDatabaseName("IX_ProductionOrders_Product");

            b.HasMany(x => x.Ingredients)
                .WithOne()
                .HasForeignKey(i => i.ProductionOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Ingredients).HasField("_ingredients").UsePropertyAccessMode(PropertyAccessMode.Field);

            b.HasMany(x => x.Allocations)
                .WithOne()
                .HasForeignKey(i => i.ProductionOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Allocations).HasField("_allocations").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<AppProductionOrderIngredient>(b =>
        {
            b.ToTable(ProductionDbProperties.DbTablePrefix + "OrderIngredients", ProductionDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.UnitCostSnapshot).HasPrecision(18, 4);
            b.Property(x => x.TotalCost).HasPrecision(18, 2);

            b.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_ProductionOrderIngredients_Order");
            b.HasIndex(x => x.IngredientProductId).HasDatabaseName("IX_ProductionOrderIngredients_Product");
        });

        builder.Entity<AppProductionOrderAllocation>(b =>
        {
            b.ToTable(ProductionDbProperties.DbTablePrefix + "OrderAllocations", ProductionDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_ProductionOrderAllocations_Order");
            b.HasIndex(x => x.BranchId).HasDatabaseName("IX_ProductionOrderAllocations_Branch");
            b.HasIndex(x => x.BranchProductionRequestItemId).HasDatabaseName("IX_ProductionOrderAllocations_RequestItem");
        });

        builder.Entity<AppProductionWaste>(b =>
        {
            b.ToTable(ProductionDbProperties.DbTablePrefix + "Wastes", ProductionDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.WasteType).IsRequired().HasMaxLength(32);
            b.Property(x => x.Reason).IsRequired().HasMaxLength(64);
            b.Property(x => x.Notes).HasMaxLength(1024);
            b.Property(x => x.UnitCost).HasPrecision(18, 4);
            b.Property(x => x.TotalCost).HasPrecision(18, 2);

            b.HasIndex(x => new { x.KitchenBranchId, x.RecordedAt }).HasDatabaseName("IX_ProductionWastes_KitchenDate");
            b.HasIndex(x => x.ProductId).HasDatabaseName("IX_ProductionWastes_Product");
            b.HasIndex(x => x.ProductionOrderId).HasDatabaseName("IX_ProductionWastes_Order");
            b.HasIndex(x => x.WasteType).HasDatabaseName("IX_ProductionWastes_Type");
        });
    }
}
