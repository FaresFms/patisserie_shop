using FluentValidation;
using Inventory.Branches;

namespace Inventory.Validation;

public class CreateBranchDtoValidator : AbstractValidator<CreateBranchDto>
{
    public CreateBranchDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(128).WithMessage("Name must not exceed 128 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(512).WithMessage("Address must not exceed 512 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithMessage("Phone must not exceed 32 characters.");

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class UpdateBranchDtoValidator : AbstractValidator<UpdateBranchDto>
{
    public UpdateBranchDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(128).WithMessage("Name must not exceed 128 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(512).WithMessage("Address must not exceed 512 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithMessage("Phone must not exceed 32 characters.");

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}
