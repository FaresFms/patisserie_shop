using Microsoft.EntityFrameworkCore;
using Production.Entities;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Production.EntityFrameworkCore;

[ConnectionStringName(ProductionDbProperties.ConnectionStringName)]
public class ProductionDbContext : AbpDbContext<ProductionDbContext>, IProductionDbContext
{
    public DbSet<AppProductionFormula> ProductionFormulas { get; set; } = null!;
    public DbSet<AppBranchProductionRequest> BranchProductionRequests { get; set; } = null!;
    public DbSet<AppProductionPlan> ProductionPlans { get; set; } = null!;
    public DbSet<AppProductionOrder> ProductionOrders { get; set; } = null!;
    public DbSet<AppProductionWaste> ProductionWastes { get; set; } = null!;

    public ProductionDbContext(DbContextOptions<ProductionDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureProduction();
    }
}
