using Microsoft.EntityFrameworkCore;
using Production.Entities;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Production.EntityFrameworkCore;

[ConnectionStringName(ProductionDbProperties.ConnectionStringName)]
public interface IProductionDbContext : IEfCoreDbContext
{
    DbSet<AppProductionFormula> ProductionFormulas { get; }
    DbSet<AppBranchProductionRequest> BranchProductionRequests { get; }
    DbSet<AppProductionPlan> ProductionPlans { get; }
    DbSet<AppProductionOrder> ProductionOrders { get; }
    DbSet<AppProductionWaste> ProductionWastes { get; }
}
