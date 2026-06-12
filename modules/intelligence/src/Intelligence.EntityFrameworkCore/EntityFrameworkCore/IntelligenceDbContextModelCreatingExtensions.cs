using Intelligence.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Intelligence.EntityFrameworkCore;

public static class IntelligenceDbContextModelCreatingExtensions
{
    public static void ConfigureIntelligence(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<AppInventoryRule>(b =>
        {
            b.ToTable(IntelligenceDbProperties.DbTablePrefix + "InventoryRules", IntelligenceDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.RuleName).IsRequired().HasMaxLength(128);
            b.Property(x => x.RuleType).IsRequired().HasMaxLength(32);
            b.Property(x => x.SuggestedAction).HasMaxLength(256);
            // NOTE: no HasDefaultValue here — same reasoning as DecisionLogs.Status below:
            // a store default makes the column ValueGeneratedOnAdd and breaks disconnected
            // updates. The entity initialises ActionMode to SuggestOnly in code.
            b.Property(x => x.ActionMode).IsRequired().HasMaxLength(32);
            b.HasIndex(x => x.RuleType).HasDatabaseName("IX_InventoryRules_Type");
            b.HasIndex(x => x.IsActive).HasDatabaseName("IX_InventoryRules_Active");
            b.HasIndex(x => x.Priority).HasDatabaseName("IX_InventoryRules_Priority");
        });

        builder.Entity<AppDecisionLog>(b =>
        {
            b.ToTable(IntelligenceDbProperties.DbTablePrefix + "DecisionLogs", IntelligenceDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.DecisionType).IsRequired().HasMaxLength(32);
            b.Property(x => x.Reasoning).IsRequired().HasMaxLength(1024);
            b.Property(x => x.SuggestedAction).HasMaxLength(256);
            // NOTE: do NOT use HasDefaultValue("Pending") here. A store default marks the
            // property ValueGeneratedOnAdd, and ABP's disconnected UpdateAsync path then
            // omits it from the UPDATE SET clause — so Acknowledge/Dismiss/Execute would
            // silently fail to persist Status (the row stays "Pending"). The entity already
            // initialises Status to Pending in code, so a DB default is unnecessary.
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);

            // Link to the corrective document created on execution (nullable —
            // only set during the single Pending→Executed transition).
            b.Property(x => x.ExecutedActionType).HasMaxLength(32);
            b.Property(x => x.ExecutedActionId);

            // Write-once outcome recorded ~48h after creation by the
            // DecisionOutcomeScanner (null = not yet evaluated). No index needed:
            // the scanner's "Outcome IS NULL AND CreationTime < cutoff" scan is
            // cheap at this table's scale.
            b.Property(x => x.Outcome).HasMaxLength(32);
            b.Property(x => x.OutcomeEvaluatedAt);

            // Nullable get-only auto-properties are silently skipped by EF's convention,
            // so map them explicitly. Without this, queries that reference BranchId etc.
            // fail with "translation of member ... failed; commonly occurs when unmapped".
            b.Property(x => x.BranchId);
            b.Property(x => x.SourceBranchId);
            b.Property(x => x.TargetBranchId);
            b.Property(x => x.StockAtEvaluation);
            b.Property(x => x.DaysWithoutSale);

            b.HasIndex(x => x.Status).HasDatabaseName("IX_DecisionLogs_Status");
            b.HasIndex(x => x.DecisionType).HasDatabaseName("IX_DecisionLogs_Type");
            b.HasIndex(x => x.ProductId).HasDatabaseName("IX_DecisionLogs_Product");
            b.HasIndex(x => x.RuleId).HasDatabaseName("IX_DecisionLogs_Rule");
            b.HasIndex(x => x.BranchId).HasDatabaseName("IX_DecisionLogs_Branch");
        });

        builder.Entity<AppProductVelocity>(b =>
        {
            b.ToTable(IntelligenceDbProperties.DbTablePrefix + "ProductVelocities", IntelligenceDbProperties.DbSchema);
            b.ConfigureByConvention();

            b.Property(x => x.AvgDailySales7).HasPrecision(9, 2);
            b.Property(x => x.AvgDailySales30).HasPrecision(9, 2);
            b.Property(x => x.Revenue30).HasPrecision(18, 2);
            b.Property(x => x.AbcClass).IsRequired().HasMaxLength(1);

            // Per-weekday demand indices (Sunday-first). NOTE: no HasDefaultValue — a
            // store default makes the column ValueGeneratedOnAdd and breaks disconnected
            // updates (same reasoning as DecisionLogs.Status). The entity initialises
            // each index to 1.0 in code.
            b.Property(x => x.WeekdayIndexSun).HasPrecision(5, 2);
            b.Property(x => x.WeekdayIndexMon).HasPrecision(5, 2);
            b.Property(x => x.WeekdayIndexTue).HasPrecision(5, 2);
            b.Property(x => x.WeekdayIndexWed).HasPrecision(5, 2);
            b.Property(x => x.WeekdayIndexThu).HasPrecision(5, 2);
            b.Property(x => x.WeekdayIndexFri).HasPrecision(5, 2);
            b.Property(x => x.WeekdayIndexSat).HasPrecision(5, 2);

            b.HasIndex(x => new { x.ProductId, x.BranchId })
                .IsUnique()
                .HasDatabaseName("IX_ProductVelocities_Product_Branch");
            b.HasIndex(x => x.BranchId).HasDatabaseName("IX_ProductVelocities_Branch");
        });
    }
}
