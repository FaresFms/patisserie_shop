using Intelligence.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Intelligence.EntityFrameworkCore;

[ConnectionStringName(IntelligenceDbProperties.ConnectionStringName)]
public interface IIntelligenceDbContext : IEfCoreDbContext
{
    DbSet<AppInventoryRule> InventoryRules { get; }
    DbSet<AppDecisionLog> DecisionLogs { get; }
    DbSet<AppProductVelocity> ProductVelocities { get; }
}
