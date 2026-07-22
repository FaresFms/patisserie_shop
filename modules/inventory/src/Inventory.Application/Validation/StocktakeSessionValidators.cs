using FluentValidation;
using Inventory.Stocktakes;

namespace Inventory.Validation;

public class StartStocktakeSessionDtoValidator : AbstractValidator<StartStocktakeSessionDto>
{
    public StartStocktakeSessionDtoValidator()
    {
        RuleFor(input => input.BranchId).NotEmpty();
    }
}

public class StocktakeSessionDraftLineDtoValidator : AbstractValidator<StocktakeSessionDraftLineDto>
{
    public StocktakeSessionDraftLineDtoValidator()
    {
        RuleFor(line => line.LineId).NotEmpty();
        RuleFor(line => line.CountedQuantity).GreaterThanOrEqualTo(0).When(line => line.CountedQuantity.HasValue);
        RuleFor(line => line.Reason)
            .Must(StocktakeVarianceReasons.IsValid)
            .When(line => !string.IsNullOrWhiteSpace(line.Reason));
        RuleFor(line => line.ReasonNotes).MaximumLength(120);
    }
}

public class SaveStocktakeSessionDraftDtoValidator : AbstractValidator<SaveStocktakeSessionDraftDto>
{
    public SaveStocktakeSessionDraftDtoValidator()
    {
        RuleFor(input => input.Id).NotEmpty();
        RuleFor(input => input.ConcurrencyStamp).NotEmpty();
        RuleFor(input => input.Notes).MaximumLength(300);
        RuleFor(input => input.Lines).NotEmpty();
        RuleForEach(input => input.Lines).SetValidator(new StocktakeSessionDraftLineDtoValidator());
    }
}

public class SubmitStocktakeSessionDtoValidator : AbstractValidator<SubmitStocktakeSessionDto>
{
    public SubmitStocktakeSessionDtoValidator()
    {
        RuleFor(input => input.Id).NotEmpty();
        RuleFor(input => input.ConcurrencyStamp).NotEmpty();
        RuleFor(input => input.Notes).NotEmpty().MaximumLength(300);
        RuleFor(input => input.Lines).NotEmpty();
        RuleForEach(input => input.Lines).SetValidator(new StocktakeSessionDraftLineDtoValidator());
    }
}

public class ReviewStocktakeSessionDtoValidator : AbstractValidator<ReviewStocktakeSessionDto>
{
    public ReviewStocktakeSessionDtoValidator()
    {
        RuleFor(input => input.Id).NotEmpty();
        RuleFor(input => input.ConcurrencyStamp).NotEmpty();
        RuleFor(input => input.ReviewNotes).MaximumLength(300);
    }
}

public class RejectStocktakeSessionDtoValidator : AbstractValidator<RejectStocktakeSessionDto>
{
    public RejectStocktakeSessionDtoValidator()
    {
        RuleFor(input => input.Id).NotEmpty();
        RuleFor(input => input.ConcurrencyStamp).NotEmpty();
        RuleFor(input => input.ReviewNotes).NotEmpty().MaximumLength(300);
    }
}

public class CancelStocktakeSessionDtoValidator : AbstractValidator<CancelStocktakeSessionDto>
{
    public CancelStocktakeSessionDtoValidator()
    {
        RuleFor(input => input.Id).NotEmpty();
        RuleFor(input => input.ConcurrencyStamp).NotEmpty();
    }
}

public class GetStocktakeSessionsInputValidator : AbstractValidator<GetStocktakeSessionsInput>
{
    public GetStocktakeSessionsInputValidator()
    {
        RuleFor(input => input.BranchId).NotEmpty();
        RuleFor(input => input.Status)
            .Must(StocktakeSessionStatuses.IsValid)
            .When(input => !string.IsNullOrWhiteSpace(input.Status));
    }
}
