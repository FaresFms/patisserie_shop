using Intelligence.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Intelligence.EntityFrameworkCore;

[ConnectionStringName(IntelligenceDbProperties.ConnectionStringName)]
public class IntelligenceDbContext : AbpDbContext<IntelligenceDbContext>, IIntelligenceDbContext
{
    public DbSet<AppInventoryRule> InventoryRules { get; set; } = null!;
    public DbSet<AppDecisionLog> DecisionLogs { get; set; } = null!;

    public IntelligenceDbContext(DbContextOptions<IntelligenceDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureIntelligence();
    }
}
