using Microsoft.EntityFrameworkCore;
using Operations.Entities;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Operations.EntityFrameworkCore;

[ConnectionStringName(OperationsDbProperties.ConnectionStringName)]
public class OperationsDbContext : AbpDbContext<OperationsDbContext>, IOperationsDbContext
{
    public DbSet<AppStockTransfer> StockTransfers { get; set; } = null!;
    public DbSet<AppPurchaseOrder> PurchaseOrders { get; set; } = null!;
    public DbSet<AppSale> Sales { get; set; } = null!;

    public OperationsDbContext(DbContextOptions<OperationsDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureOperations();
    }
}
