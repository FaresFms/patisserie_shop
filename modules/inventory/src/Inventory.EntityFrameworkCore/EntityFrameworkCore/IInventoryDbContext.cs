using Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Inventory.EntityFrameworkCore;

[ConnectionStringName(InventoryDbProperties.ConnectionStringName)]
public interface IInventoryDbContext : IEfCoreDbContext
{
    DbSet<AppCategory> Categories { get; }
    DbSet<AppSupplier> Suppliers { get; }
    DbSet<AppProduct> Products { get; }
    DbSet<AppBranch> Branches { get; }
    DbSet<AppBranchInventory> BranchInventories { get; }
    DbSet<AppStockMovement> StockMovements { get; }
}
