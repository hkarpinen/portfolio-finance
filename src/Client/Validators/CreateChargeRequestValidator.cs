using Finance.Application.Commands;
using Finance.Domain.ValueObjects;
using FluentValidation;

namespace Client.Validators;

public sealed class CreateChargeRequestValidator : AbstractValidator<CreateChargeCommand>
{
    public CreateChargeRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        // The DTO keeps Category as a free string (API compatibility), but it must name a real
        // ChargeCategory (case-insensitive) — the manager already falls back to Other, so this only
        // rejects genuinely bogus input rather than silently coercing it.
        RuleFor(x => x.Category).NotEmpty().Must(BeAChargeCategory)
            .WithMessage("Category must be a valid charge category.");
        RuleFor(x => x.DueDate).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.RecurrenceEndDate)
            .GreaterThanOrEqualTo(x => x.RecurrenceStartDate!.Value)
            .When(x => x.RecurrenceStartDate.HasValue && x.RecurrenceEndDate.HasValue);
    }

    internal static bool BeAChargeCategory(string? category) =>
        Enum.TryParse<ChargeCategory>(category, ignoreCase: true, out _);
}

public sealed class UpdateChargeRequestValidator : AbstractValidator<UpdateChargeCommand>
{
    public UpdateChargeRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Category).NotEmpty().Must(CreateChargeRequestValidator.BeAChargeCategory)
            .WithMessage("Category must be a valid charge category.");
        RuleFor(x => x.DueDate).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
    }
}
