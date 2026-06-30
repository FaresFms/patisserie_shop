using Production.Entities;
using Production.BranchRequests;
using Production.Formulas;
using Production.Plans;
using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;

namespace Production;

[Mapper]
public partial class ProductionFormulaToDtoMapper : MapperBase<AppProductionFormula, ProductionFormulaDto>
{
    // FinishedProductName / FinishedProductUnit are joined from Inventory and overlaid
    // manually by the app service. Items map by convention (same field names).
    [MapperIgnoreTarget(nameof(ProductionFormulaDto.FinishedProductName))]
    [MapperIgnoreTarget(nameof(ProductionFormulaDto.FinishedProductUnit))]
    public override partial ProductionFormulaDto Map(AppProductionFormula source);

    [MapperIgnoreTarget(nameof(ProductionFormulaDto.FinishedProductName))]
    [MapperIgnoreTarget(nameof(ProductionFormulaDto.FinishedProductUnit))]
    public override partial void Map(AppProductionFormula source, ProductionFormulaDto destination);
}

[Mapper]
public partial class ProductionFormulaItemToDtoMapper : MapperBase<AppProductionFormulaItem, ProductionFormulaItemDto>
{
    public override partial ProductionFormulaItemDto Map(AppProductionFormulaItem source);
    public override partial void Map(AppProductionFormulaItem source, ProductionFormulaItemDto destination);
}

[Mapper]
public partial class ProductionFormulaListItemToDtoMapper : MapperBase<ProductionFormulaListItem, ProductionFormulaListItemDto>
{
    public override partial ProductionFormulaListItemDto Map(ProductionFormulaListItem source);
    public override partial void Map(ProductionFormulaListItem source, ProductionFormulaListItemDto destination);
}

[Mapper]
public partial class ProductionProductLookupToDtoMapper : MapperBase<ProductionProductLookup, ProductLookupDto>
{
    public override partial ProductLookupDto Map(ProductionProductLookup source);
    public override partial void Map(ProductionProductLookup source, ProductLookupDto destination);
}

[Mapper]
public partial class BranchProductionRequestToDtoMapper : MapperBase<AppBranchProductionRequest, BranchProductionRequestDto>
{
    [MapperIgnoreTarget(nameof(BranchProductionRequestDto.BranchName))]
    public override partial BranchProductionRequestDto Map(AppBranchProductionRequest source);

    [MapperIgnoreTarget(nameof(BranchProductionRequestDto.BranchName))]
    public override partial void Map(AppBranchProductionRequest source, BranchProductionRequestDto destination);
}

[Mapper]
public partial class BranchProductionRequestItemToDtoMapper : MapperBase<AppBranchProductionRequestItem, BranchProductionRequestItemDto>
{
    [MapperIgnoreTarget(nameof(BranchProductionRequestItemDto.ProductName))]
    [MapperIgnoreTarget(nameof(BranchProductionRequestItemDto.ProductSku))]
    [MapperIgnoreTarget(nameof(BranchProductionRequestItemDto.ProductUnit))]
    public override partial BranchProductionRequestItemDto Map(AppBranchProductionRequestItem source);

    [MapperIgnoreTarget(nameof(BranchProductionRequestItemDto.ProductName))]
    [MapperIgnoreTarget(nameof(BranchProductionRequestItemDto.ProductSku))]
    [MapperIgnoreTarget(nameof(BranchProductionRequestItemDto.ProductUnit))]
    public override partial void Map(AppBranchProductionRequestItem source, BranchProductionRequestItemDto destination);
}

[Mapper]
public partial class BranchProductionRequestListItemToDtoMapper : MapperBase<BranchProductionRequestListItem, BranchProductionRequestListItemDto>
{
    public override partial BranchProductionRequestListItemDto Map(BranchProductionRequestListItem source);
    public override partial void Map(BranchProductionRequestListItem source, BranchProductionRequestListItemDto destination);
}

[Mapper]
public partial class ProductionPlanToDtoMapper : MapperBase<AppProductionPlan, ProductionPlanDto>
{
    [MapperIgnoreTarget(nameof(ProductionPlanDto.KitchenBranchName))]
    public override partial ProductionPlanDto Map(AppProductionPlan source);

    [MapperIgnoreTarget(nameof(ProductionPlanDto.KitchenBranchName))]
    public override partial void Map(AppProductionPlan source, ProductionPlanDto destination);
}

[Mapper]
public partial class ProductionPlanLineToDtoMapper : MapperBase<AppProductionPlanLine, ProductionPlanLineDto>
{
    [MapperIgnoreTarget(nameof(ProductionPlanLineDto.ProductName))]
    [MapperIgnoreTarget(nameof(ProductionPlanLineDto.ProductSku))]
    [MapperIgnoreTarget(nameof(ProductionPlanLineDto.ProductUnit))]
    public override partial ProductionPlanLineDto Map(AppProductionPlanLine source);

    [MapperIgnoreTarget(nameof(ProductionPlanLineDto.ProductName))]
    [MapperIgnoreTarget(nameof(ProductionPlanLineDto.ProductSku))]
    [MapperIgnoreTarget(nameof(ProductionPlanLineDto.ProductUnit))]
    public override partial void Map(AppProductionPlanLine source, ProductionPlanLineDto destination);
}

[Mapper]
public partial class ProductionPlanListItemToDtoMapper : MapperBase<ProductionPlanListItem, ProductionPlanListItemDto>
{
    public override partial ProductionPlanListItemDto Map(ProductionPlanListItem source);
    public override partial void Map(ProductionPlanListItem source, ProductionPlanListItemDto destination);
}
