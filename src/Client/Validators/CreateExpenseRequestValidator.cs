using Finance.Application.Commands;
using Finance.Domain.ValueObjects;
using FluentValidation;

namespace Client.Validators;

public sealed class CreateExpenseRequestValidator : AbstractValidator<CreateExpenseCommand>
{
    public CreateExpenseRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        // Category stays a free string on the DTO for API compatibility, but must name a real
        // ExpenseCategory (case-insensitive). The manager already falls back to Other, so this rule only
        // rejects genuinely bogus input instead of silently coercing it.
        RuleFor(x => x.Category).NotEmpty().Must(BeAExpenseCategory)
            .WithMessage("Category must be a valid expense category.");
        RuleFor(x => x.DueDate).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.RecurrenceEndDate)
            .GreaterThanOrEqualTo(x => x.RecurrenceStartDate!.Value)
            .When(x => x.RecurrenceStartDate.HasValue && x.RecurrenceEndDate.HasValue);
    }

    internal static bool BeAExpenseCategory(string? category) => ExpenseCategories.IsKnown(category);
}

public sealed class UpdateExpenseRequestValidator : AbstractValidator<UpdateExpenseCommand>
{
    public UpdateExpenseRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Category).NotEmpty().Must(CreateExpenseRequestValidator.BeAExpenseCategory)
            .WithMessage("Category must be a valid expense category.");
        RuleFor(x => x.DueDate).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
    }
}
