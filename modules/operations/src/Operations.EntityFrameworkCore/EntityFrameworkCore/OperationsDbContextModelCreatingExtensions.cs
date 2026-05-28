using Microsoft.EntityFrameworkCore;
using Operations.Entities;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Operations.EntityFrameworkCore;

public static class OperationsDbContextModelCreatingExtensions
{
    public static void ConfigureOperations(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<AppStockTransfer>(b =>
        {
            b.ToTable(OperationsDbProperties.DbTablePrefix + "StockTransfers", OperationsDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);
            b.Property(x => x.Notes).HasMaxLength(512);

            b.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(i => i.StockTransferId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<AppStockTransferItem>(b =>
        {
            b.ToTable(OperationsDbProperties.DbTablePrefix + "StockTransferItems", OperationsDbProperties.DbSchema);
            b.ConfigureByConvention();
        });

        builder.Entity<AppPurchaseOrder>(b =>
        {
            b.ToTable(OperationsDbProperties.DbTablePrefix + "PurchaseOrders", OperationsDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.PONumber).IsRequired().HasMaxLength(64);
            b.Property(x => x.Status).IsRequired().HasMaxLength(32);
            b.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("USD");
            b.Property(x => x.Notes).HasMaxLength(512);
            b.HasIndex(x => x.PONumber).IsUnique();
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.OrderDate);

            b.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(i => i.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<AppPurchaseOrderItem>(b =>
        {
            b.ToTable(OperationsDbProperties.DbTablePrefix + "PurchaseOrderItems", OperationsDbProperties.DbSchema);
            b.ConfigureByConvention();
        });

        builder.Entity<AppSale>(b =>
        {
            b.ToTable(OperationsDbProperties.DbTablePrefix + "Sales", OperationsDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.InvoiceNumber).IsRequired().HasMaxLength(64);
            b.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("USD");
            b.Property(x => x.Notes).HasMaxLength(512);
            b.HasIndex(x => x.InvoiceNumber).IsUnique();
            b.HasIndex(x => x.BranchId);
            b.HasIndex(x => x.SaleDate);

            b.HasMany(x => x.Items)
                .WithOne()
                .HasForeignKey(i => i.SaleId)
                .OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<AppSaleItem>(b =>
        {
            b.ToTable(OperationsDbProperties.DbTablePrefix + "SaleItems", OperationsDbProperties.DbSchema);
            b.ConfigureByConvention();
        });
    }
}
