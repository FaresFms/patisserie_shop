using Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Inventory.EntityFrameworkCore;

[ConnectionStringName(InventoryDbProperties.ConnectionStringName)]
public class InventoryDbContext : AbpDbContext<InventoryDbContext>, IInventoryDbContext
{
    public DbSet<AppCategory> Categories { get; set; } = null!;
    public DbSet<AppSupplier> Suppliers { get; set; } = null!;
    public DbSet<AppProduct> Products { get; set; } = null!;
    public DbSet<AppBranch> Branches { get; set; } = null!;
    public DbSet<AppBranchInventory> BranchInventories { get; set; } = null!;
    public DbSet<AppStockMovement> StockMovements { get; set; } = null!;

    public InventoryDbContext(DbContextOptions<InventoryDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureInventory();
    }
}
