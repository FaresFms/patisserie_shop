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
    }
}
