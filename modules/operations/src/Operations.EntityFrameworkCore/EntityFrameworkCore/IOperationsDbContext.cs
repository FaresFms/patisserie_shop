using Microsoft.EntityFrameworkCore;
using Operations.Entities;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;

namespace Operations.EntityFrameworkCore;

[ConnectionStringName(OperationsDbProperties.ConnectionStringName)]
public interface IOperationsDbContext : IEfCoreDbContext
{
    DbSet<AppStockTransfer> StockTransfers { get; }
    DbSet<AppPurchaseOrder> PurchaseOrders { get; }
    DbSet<AppSale> Sales { get; }
}
