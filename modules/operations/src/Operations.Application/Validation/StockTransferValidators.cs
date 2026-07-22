using FluentValidation;
using Microsoft.Extensions.Localization;
using Operations.Localization;
using Operations.StockTransfers;

namespace Operations.Validation;

public class CreateStockTransferDtoValidator : AbstractValidator<CreateStockTransferDto>
{
    public CreateStockTransferDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.ToBranchId)
            .NotEmpty().WithMessage(_ => localizer["Validation:DestinationBranchRequired"]);

        RuleFor(x => x)
            .Must(x => !x.FromBranchId.HasValue || x.FromBranchId != x.ToBranchId)
            .WithMessage(_ => localizer["Validation:SourceDestinationDifferent"]);

        RuleFor(x => x.RequestedDate)
            .NotEmpty().WithMessage(_ => localizer["Validation:RequestedDateRequired"]);

        RuleFor(x => x.Notes)
            .MaximumLength(512).WithMessage(_ => localizer["Validation:NotesMax512"]);
    }
}

public class AssignStockTransferSourceDtoValidator : AbstractValidator<AssignStockTransferSourceDto>
{
    public AssignStockTransferSourceDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.FromBranchId)
            .NotEmpty().WithMessage(_ => localizer["Validation:SourceBranchRequired"]);
    }
}

public class AddStockTransferItemDtoValidator : AbstractValidator<AddStockTransferItemDto>
{
    public AddStockTransferItemDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage(_ => localizer["Validation:ProductRequired"]);

        RuleFor(x => x.RequestedQuantity)
            .GreaterThan(0).WithMessage(_ => localizer["Validation:RequestedQuantityPositive"]);
    }
}

public class UpdateApprovedQuantityDtoValidator : AbstractValidator<UpdateApprovedQuantityDto>
{
    public UpdateApprovedQuantityDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.ApprovedQuantity)
            .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:ApprovedQuantityNonNegative"]);
    }
}

public class CompleteStockTransferDtoValidator : AbstractValidator<CompleteStockTransferDto>
{
    public CompleteStockTransferDtoValidator(IStringLocalizer<OperationsResource> localizer)
    {
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage(_ => localizer["Validation:TransferLinesRequired"]);

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId)
                .NotEmpty().WithMessage(_ => localizer["Validation:TransferItemRequired"]);

            line.RuleFor(l => l.TransferredQuantity)
                .GreaterThanOrEqualTo(0).WithMessage(_ => localizer["Validation:TransferredQuantityNonNegative"]);
        });
    }
}
