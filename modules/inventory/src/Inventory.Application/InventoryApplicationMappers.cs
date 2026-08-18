using Inventory.BranchInventory;
using Inventory.Branches;
using Inventory.Categories;
using Inventory.Entities;
using Inventory.Products;
using Inventory.StockBatches;
using Inventory.StockMovements;
using Inventory.Stocktakes;
using Inventory.Suppliers;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Inventory;

[Mapper]
public partial class CategoryToDtoMapper : MapperBase<AppCategory, CategoryDto>
{
    [MapperIgnoreTarget(nameof(CategoryDto.Name))]
    [MapperIgnoreTarget(nameof(CategoryDto.Description))]
    public override partial CategoryDto Map(AppCategory source);
    [MapperIgnoreTarget(nameof(CategoryDto.Name))]
    [MapperIgnoreTarget(nameof(CategoryDto.Description))]
    public override partial void Map(AppCategory source, CategoryDto destination);
}

[Mapper]
public partial class SupplierToDtoMapper : MapperBase<AppSupplier, SupplierDto>
{
    public override partial SupplierDto Map(AppSupplier source);
    public override partial void Map(AppSupplier source, SupplierDto destination);
}

[Mapper]
public partial class ProductToDtoMapper : MapperBase<AppProduct, ProductDto>
{
    [MapperIgnoreTarget(nameof(ProductDto.Name))]
    [MapperIgnoreTarget(nameof(ProductDto.Description))]
    [MapperIgnoreTarget(nameof(ProductDto.Unit))]
    public override partial ProductDto Map(AppProduct source);
    [MapperIgnoreTarget(nameof(ProductDto.Name))]
    [MapperIgnoreTarget(nameof(ProductDto.Description))]
    [MapperIgnoreTarget(nameof(ProductDto.Unit))]
    public override partial void Map(AppProduct source, ProductDto destination);
}

[Mapper]
public partial class ProductToLookupMapper : MapperBase<AppProduct, ProductLookupDto>
{
    [MapperIgnoreTarget(nameof(ProductLookupDto.Name))]
    [MapperIgnoreTarget(nameof(ProductLookupDto.Unit))]
    public override partial ProductLookupDto Map(AppProduct source);
    [MapperIgnoreTarget(nameof(ProductLookupDto.Name))]
    [MapperIgnoreTarget(nameof(ProductLookupDto.Unit))]
    public override partial void Map(AppProduct source, ProductLookupDto destination);
}

[Mapper]
public partial class BranchToDtoMapper : MapperBase<AppBranch, BranchDto>
{
    [MapperIgnoreTarget(nameof(BranchDto.Name))]
    [MapperIgnoreTarget(nameof(BranchDto.Address))]
    public override partial BranchDto Map(AppBranch source);
    [MapperIgnoreTarget(nameof(BranchDto.Name))]
    [MapperIgnoreTarget(nameof(BranchDto.Address))]
    public override partial void Map(AppBranch source, BranchDto destination);
}

[Mapper]
public partial class BranchInventoryToDtoMapper : MapperBase<AppBranchInventory, BranchInventoryDto>
{
    [MapperIgnoreTarget(nameof(BranchInventoryDto.ProductName))]
    [MapperIgnoreTarget(nameof(BranchInventoryDto.ProductSKU))]
    [MapperIgnoreTarget(nameof(BranchInventoryDto.ProductUnit))]
    [MapperIgnoreTarget(nameof(BranchInventoryDto.ProductIsActive))]
    public override partial BranchInventoryDto Map(AppBranchInventory source);

    [MapperIgnoreTarget(nameof(BranchInventoryDto.ProductName))]
    [MapperIgnoreTarget(nameof(BranchInventoryDto.ProductSKU))]
    [MapperIgnoreTarget(nameof(BranchInventoryDto.ProductUnit))]
    [MapperIgnoreTarget(nameof(BranchInventoryDto.ProductIsActive))]
    public override partial void Map(AppBranchInventory source, BranchInventoryDto destination);
}

[Mapper]
public partial class StockMovementToDtoMapper : MapperBase<AppStockMovement, StockMovementDto>
{
    [MapperIgnoreTarget(nameof(StockMovementDto.BranchName))]
    [MapperIgnoreTarget(nameof(StockMovementDto.ProductName))]
    [MapperIgnoreTarget(nameof(StockMovementDto.ProductSKU))]
    [MapperIgnoreTarget(nameof(StockMovementDto.ProductUnit))]
    public override partial StockMovementDto Map(AppStockMovement source);

    [MapperIgnoreTarget(nameof(StockMovementDto.BranchName))]
    [MapperIgnoreTarget(nameof(StockMovementDto.ProductName))]
    [MapperIgnoreTarget(nameof(StockMovementDto.ProductSKU))]
    [MapperIgnoreTarget(nameof(StockMovementDto.ProductUnit))]
    public override partial void Map(AppStockMovement source, StockMovementDto destination);
}

[Mapper]
public partial class BranchToLookupMapper : MapperBase<AppBranch, BranchLookupDto>
{
    [MapperIgnoreTarget(nameof(BranchLookupDto.Name))]
    public override partial BranchLookupDto Map(AppBranch source);
    [MapperIgnoreTarget(nameof(BranchLookupDto.Name))]
    public override partial void Map(AppBranch source, BranchLookupDto destination);
}

[Mapper]
public partial class StockBatchToDtoMapper : MapperBase<AppStockBatch, StockBatchDto>
{
    [MapperIgnoreTarget(nameof(StockBatchDto.ProductName))]
    [MapperIgnoreTarget(nameof(StockBatchDto.ProductSKU))]
    [MapperIgnoreTarget(nameof(StockBatchDto.ProductUnit))]
    [MapperIgnoreTarget(nameof(StockBatchDto.BranchName))]
    public override partial StockBatchDto Map(AppStockBatch source);

    [MapperIgnoreTarget(nameof(StockBatchDto.ProductName))]
    [MapperIgnoreTarget(nameof(StockBatchDto.ProductSKU))]
    [MapperIgnoreTarget(nameof(StockBatchDto.ProductUnit))]
    [MapperIgnoreTarget(nameof(StockBatchDto.BranchName))]
    public override partial void Map(AppStockBatch source, StockBatchDto destination);
}

[Mapper]
public partial class StocktakeSessionLineToDtoMapper : MapperBase<AppStocktakeLine, StocktakeSessionLineDto>
{
    public override partial StocktakeSessionLineDto Map(AppStocktakeLine source);
    public override partial void Map(AppStocktakeLine source, StocktakeSessionLineDto destination);
}

[Mapper]
public partial class StocktakeSessionToDtoMapper : MapperBase<AppStocktakeSession, StocktakeSessionDto>
{
    [MapperIgnoreTarget(nameof(StocktakeSessionDto.Lines))]
    public override partial StocktakeSessionDto Map(AppStocktakeSession source);

    [MapperIgnoreTarget(nameof(StocktakeSessionDto.Lines))]
    public override partial void Map(AppStocktakeSession source, StocktakeSessionDto destination);
}

[Mapper]
public partial class StocktakeSessionToListDtoMapper : MapperBase<AppStocktakeSession, StocktakeSessionListDto>
{
    public override partial StocktakeSessionListDto Map(AppStocktakeSession source);
    public override partial void Map(AppStocktakeSession source, StocktakeSessionListDto destination);
}
