using System;
using FluentValidation;
using Inventory.BranchInventory;

namespace Inventory.Validation;

public class AdjustStockDtoValidator : AbstractValidator<AdjustStockDto>
{
    public AdjustStockDtoValidator()
    {
        RuleFor(x => x.NewQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("New quantity must be 0 or greater.");

        RuleFor(x => x.MovementType)
            .NotEmpty().WithMessage("Movement type is required.")
            .Must(StockMovementTypes.IsValid)
            .WithMessage("Movement type must be one of: Purchase, Sale, TransferIn, TransferOut, ManualAdjustment.");

        RuleFor(x => x.Notes)
            .MaximumLength(512).WithMessage("Notes must not exceed 512 characters.");

        RuleFor(x => x.ProductionDate)
            // One-day slack absorbs the local-time vs UTC gap for shops ahead of UTC.
            .Must(d => !d.HasValue || d.Value.Date <= DateTime.UtcNow.Date.AddDays(1))
            .WithMessage("Production date cannot be in the future.");
    }
}

public class InitializeBranchInventoryDtoValidator : AbstractValidator<InitializeBranchInventoryDto>
{
    public InitializeBranchInventoryDtoValidator()
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch is required.");

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product is required.");

        RuleFor(x => x.InitialQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Initial quantity must be 0 or greater.");

        RuleFor(x => x.MinimumStock)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum stock must be 0 or greater.");

        RuleFor(x => x.MaximumStock)
            .GreaterThanOrEqualTo(0).WithMessage("Maximum stock must be 0 or greater.")
            .When(x => x.MaximumStock.HasValue);

        RuleFor(x => x)
            .Must(x => !x.MaximumStock.HasValue || x.MaximumStock.Value >= x.MinimumStock)
            .WithMessage("Maximum stock must be greater than or equal to minimum stock.");
    }
}

public class UpdateStockLimitsDtoValidator : AbstractValidator<UpdateStockLimitsDto>
{
    public UpdateStockLimitsDtoValidator()
    {
        RuleFor(x => x.MinimumStock)
            .GreaterThanOrEqualTo(0).WithMessage("Minimum stock must be 0 or greater.");

        RuleFor(x => x.MaximumStock)
            .GreaterThanOrEqualTo(0).WithMessage("Maximum stock must be 0 or greater.")
            .When(x => x.MaximumStock.HasValue);

        RuleFor(x => x)
            .Must(x => !x.MaximumStock.HasValue || x.MaximumStock.Value >= x.MinimumStock)
            .WithMessage("Maximum stock must be greater than or equal to minimum stock.");
    }
}
