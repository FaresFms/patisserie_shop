using Inventory.Branches;
using Inventory.Categories;
using Inventory.Entities;
using Inventory.Products;
using Inventory.Suppliers;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Inventory;

[Mapper]
public partial class CategoryToDtoMapper : MapperBase<AppCategory, CategoryDto>
{
    public override partial CategoryDto Map(AppCategory source);
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
    public override partial ProductDto Map(AppProduct source);
    public override partial void Map(AppProduct source, ProductDto destination);
}

[Mapper]
public partial class ProductToLookupMapper : MapperBase<AppProduct, ProductLookupDto>
{
    public override partial ProductLookupDto Map(AppProduct source);
    public override partial void Map(AppProduct source, ProductLookupDto destination);
}

[Mapper]
public partial class BranchToDtoMapper : MapperBase<AppBranch, BranchDto>
{
    public override partial BranchDto Map(AppBranch source);
    public override partial void Map(AppBranch source, BranchDto destination);
}

[Mapper]
public partial class BranchToLookupMapper : MapperBase<AppBranch, BranchLookupDto>
{
    public override partial BranchLookupDto Map(AppBranch source);
    public override partial void Map(AppBranch source, BranchLookupDto destination);
}
