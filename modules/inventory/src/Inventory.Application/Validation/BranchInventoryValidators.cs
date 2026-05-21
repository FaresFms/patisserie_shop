using FluentValidation;
using Inventory.BranchInventory;

namespace Inventory.Validation;

public class AdjustStockDtoValidator : AbstractValidator<AdjustStockDto>
{
    public AdjustStockDtoValidator()
    {
        RuleFor(x => x.NewQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MovementType)
            .NotEmpty()
            .Must(StockMovementTypes.IsValid)
            .WithMessage("MovementType must be one of: Purchase, Sale, TransferIn, TransferOut, ManualAdjustment.");
        RuleFor(x => x.Notes).MaximumLength(512);
    }
}

public class InitializeBranchInventoryDtoValidator : AbstractValidator<InitializeBranchInventoryDto>
{
    public InitializeBranchInventoryDtoValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.ProductId).NotEmpty();
        RuleFor(x => x.InitialQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumStock).GreaterThanOrEqualTo(0).When(x => x.MaximumStock.HasValue);
        RuleFor(x => x).Must(x => !x.MaximumStock.HasValue || x.MaximumStock.Value >= x.MinimumStock)
            .WithMessage("MaximumStock must be greater than or equal to MinimumStock.");
    }
}

public class UpdateStockLimitsDtoValidator : AbstractValidator<UpdateStockLimitsDto>
{
    public UpdateStockLimitsDtoValidator()
    {
        RuleFor(x => x.MinimumStock).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaximumStock).GreaterThanOrEqualTo(0).When(x => x.MaximumStock.HasValue);
        RuleFor(x => x).Must(x => !x.MaximumStock.HasValue || x.MaximumStock.Value >= x.MinimumStock)
            .WithMessage("MaximumStock must be greater than or equal to MinimumStock.");
    }
}
