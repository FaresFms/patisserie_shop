using System;
using FluentValidation;
using Inventory.BranchInventory;
using Inventory.Localization;
using Microsoft.Extensions.Localization;

namespace Inventory.Validation;

public class AdjustStockDtoValidator : AbstractValidator<AdjustStockDto>
{
    public AdjustStockDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.NewQuantity)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:NewQuantityNonNegative"]);

        RuleFor(x => x.MovementType)
            .NotEmpty().WithMessage(_ => localizer["Validation:MovementTypeRequired"])
            .Must(StockMovementTypes.IsValid)
            .WithMessage(_ => localizer["Validation:MovementTypeInvalid"]);

        RuleFor(x => x.Notes)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:NotesMax512"]);

        RuleFor(x => x.ProductionDate)
            // One-day slack absorbs the local-time vs UTC gap for shops ahead of UTC.
            .Must(d => !d.HasValue || d.Value.Date <= DateTime.UtcNow.Date.AddDays(1))
            .WithMessage(_ => localizer["Validation:ProductionDateFuture"]);
    }
}

public class InitializeBranchInventoryDtoValidator : AbstractValidator<InitializeBranchInventoryDto>
{
    public InitializeBranchInventoryDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage(_ => localizer["Validation:BranchRequired"]);

        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage(_ => localizer["Validation:ProductRequired"]);

        RuleFor(x => x.InitialQuantity)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:InitialQuantityNonNegative"]);

        RuleFor(x => x.MinimumStock)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:MinimumStockNonNegative"]);

        RuleFor(x => x.MaximumStock)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:MaximumStockNonNegative"])
            .When(x => x.MaximumStock.HasValue);

        RuleFor(x => x)
            .Must(x => !x.MaximumStock.HasValue || x.MaximumStock.Value >= x.MinimumStock)
            .WithMessage(_ => localizer["Validation:MaximumStockBelowMinimum"]);
    }
}

public class UpdateStockLimitsDtoValidator : AbstractValidator<UpdateStockLimitsDto>
{
    public UpdateStockLimitsDtoValidator(IStringLocalizer<InventoryResource> localizer)
    {
        RuleFor(x => x.MinimumStock)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:MinimumStockNonNegative"]);

        RuleFor(x => x.MaximumStock)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:MaximumStockNonNegative"])
            .When(x => x.MaximumStock.HasValue);

        RuleFor(x => x)
            .Must(x => !x.MaximumStock.HasValue || x.MaximumStock.Value >= x.MinimumStock)
            .WithMessage(_ => localizer["Validation:MaximumStockBelowMinimum"]);
    }
}
