using FluentValidation;
using Operations.StockTransfers;

namespace Operations.Validation;

public class CreateStockTransferDtoValidator : AbstractValidator<CreateStockTransferDto>
{
    public CreateStockTransferDtoValidator()
    {
        RuleFor(x => x.ToBranchId)
            .NotEmpty().WithMessage("Destination branch is required.");

        RuleFor(x => x)
            .Must(x => !x.FromBranchId.HasValue || x.FromBranchId != x.ToBranchId)
            .WithMessage("Source and destination branches must be different.");

        RuleFor(x => x.RequestedDate)
            .NotEmpty().WithMessage("Requested date is required.");

        RuleFor(x => x.Notes)
            .MaximumLength(512).WithMessage("Notes must not exceed 512 characters.");
    }
}

public class AssignStockTransferSourceDtoValidator : AbstractValidator<AssignStockTransferSourceDto>
{
    public AssignStockTransferSourceDtoValidator()
    {
        RuleFor(x => x.FromBranchId)
            .NotEmpty().WithMessage("Source branch is required.");
    }
}

public class AddStockTransferItemDtoValidator : AbstractValidator<AddStockTransferItemDto>
{
    public AddStockTransferItemDtoValidator()
    {
        RuleFor(x => x.ProductId)
            .NotEmpty().WithMessage("Product is required.");

        RuleFor(x => x.RequestedQuantity)
            .GreaterThan(0).WithMessage("Requested quantity must be greater than 0.");
    }
}

public class UpdateApprovedQuantityDtoValidator : AbstractValidator<UpdateApprovedQuantityDto>
{
    public UpdateApprovedQuantityDtoValidator()
    {
        RuleFor(x => x.ApprovedQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Approved quantity must be 0 or greater.");
    }
}

public class CompleteStockTransferDtoValidator : AbstractValidator<CompleteStockTransferDto>
{
    public CompleteStockTransferDtoValidator()
    {
        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("At least one line must be specified.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ItemId)
                .NotEmpty().WithMessage("Item is required.");

            line.RuleFor(l => l.TransferredQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("Transferred quantity must be 0 or greater.");
        });
    }
}
