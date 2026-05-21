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
            b.Property(x => x.Status).IsRequired().HasMaxLength(32).HasDefaultValue("Pending");
            b.HasIndex(x => x.Status).HasDatabaseName("IX_DecisionLogs_Status");
            b.HasIndex(x => x.DecisionType).HasDatabaseName("IX_DecisionLogs_Type");
            b.HasIndex(x => x.ProductId).HasDatabaseName("IX_DecisionLogs_Product");
            b.HasIndex(x => x.RuleId).HasDatabaseName("IX_DecisionLogs_Rule");
        });
    }
}
