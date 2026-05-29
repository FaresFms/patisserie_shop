using Inventory.Entities;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace Inventory.EntityFrameworkCore;

public static class InventoryDbContextModelCreatingExtensions
{
    public static void ConfigureInventory(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        builder.Entity<AppCategory>(b =>
        {
            b.ToTable(InventoryDbProperties.DbTablePrefix + "Categories", InventoryDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.Description).HasMaxLength(512);
        });

        builder.Entity<AppSupplier>(b =>
        {
            b.ToTable(InventoryDbProperties.DbTablePrefix + "Suppliers", InventoryDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.ContactPerson).HasMaxLength(128);
            b.Property(x => x.Phone).HasMaxLength(32);
            b.Property(x => x.Email).HasMaxLength(256);
            b.Property(x => x.Address).HasMaxLength(512);
        });

        builder.Entity<AppProduct>(b =>
        {
            b.ToTable(InventoryDbProperties.DbTablePrefix + "Products", InventoryDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.SKU).IsRequired().HasMaxLength(64);
            b.Property(x => x.Description).HasMaxLength(1024);
            b.Property(x => x.Unit).IsRequired().HasMaxLength(32);
            b.Property(x => x.Currency).HasMaxLength(3).HasDefaultValue("USD");
            b.Property(x => x.ImageUrl).HasMaxLength(512);
            b.HasIndex(x => x.SKU).IsUnique();
        });

        builder.Entity<AppBranch>(b =>
        {
            b.ToTable(InventoryDbProperties.DbTablePrefix + "Branches", InventoryDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.Property(x => x.Address).HasMaxLength(512);
            b.Property(x => x.Phone).HasMaxLength(32);
            b.Property(x => x.Email).HasMaxLength(256);
        });

        builder.Entity<AppBranchInventory>(b =>
        {
            b.ToTable(InventoryDbProperties.DbTablePrefix + "BranchInventories", InventoryDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
            b.HasIndex(x => new { x.BranchId, x.ProductId })
                .IsUnique()
                .HasDatabaseName("UIX_BranchInventory_Branch_Product");
        });

        builder.Entity<AppStockMovement>(b =>
        {
            b.ToTable(InventoryDbProperties.DbTablePrefix + "StockMovements", InventoryDbProperties.DbSchema);
            b.ConfigureByConvention();
            b.Property(x => x.MovementType).IsRequired().HasMaxLength(32);
            b.Property(x => x.ReferenceType).HasMaxLength(32);
            b.Property(x => x.Notes).HasMaxLength(512);
            // Getter-only properties are not auto-discovered by EF Core convention;
            // UsePropertyAccessMode(Field) tells EF Core to read/write via the
            // compiler-generated backing field (<PropertyName>k__BackingField).
            b.Property(x => x.Quantity).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Property(x => x.QuantityBefore).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Property(x => x.QuantityAfter).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.Property(x => x.ReferenceId).UsePropertyAccessMode(PropertyAccessMode.Field);
            b.HasIndex(x => x.BranchId).HasDatabaseName("IX_StockMovements_Branch");
            b.HasIndex(x => x.ProductId).HasDatabaseName("IX_StockMovements_Product");
            b.HasIndex(x => x.CreationTime).HasDatabaseName("IX_StockMovements_Date");
        });
    }
}
