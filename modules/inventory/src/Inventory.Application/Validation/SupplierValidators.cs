using FluentValidation;
using Inventory.Suppliers;

namespace Inventory.Validation;

public class CreateSupplierDtoValidator : AbstractValidator<CreateSupplierDto>
{
    public CreateSupplierDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(128).WithMessage("Name must not exceed 128 characters.");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(128).WithMessage("Contact person must not exceed 128 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithMessage("Phone must not exceed 32 characters.");

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(512).WithMessage("Address must not exceed 512 characters.");

        RuleFor(x => x.LeadTimeDays)
            .InclusiveBetween(0, 365).WithMessage("Lead time must be between 0 and 365 days.");
    }
}

public class UpdateSupplierDtoValidator : AbstractValidator<UpdateSupplierDto>
{
    public UpdateSupplierDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(128).WithMessage("Name must not exceed 128 characters.");

        RuleFor(x => x.ContactPerson)
            .MaximumLength(128).WithMessage("Contact person must not exceed 128 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(32).WithMessage("Phone must not exceed 32 characters.");

        RuleFor(x => x.Email)
            .MaximumLength(256).WithMessage("Email must not exceed 256 characters.")
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));

        RuleFor(x => x.Address)
            .MaximumLength(512).WithMessage("Address must not exceed 512 characters.");

        RuleFor(x => x.LeadTimeDays)
            .InclusiveBetween(0, 365).WithMessage("Lead time must be between 0 and 365 days.");
    }
}
