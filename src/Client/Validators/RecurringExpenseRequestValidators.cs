using Finance.Application.Dtos;
using FluentValidation;

namespace Client.Validators;

/// <summary>
/// Lengths match the columns these land in (title 200, description 2000). Bounded here so an
/// over-long title is a 400 naming the field, not a Postgres error surfacing as a 500 — the
/// aggregate only asks whether a title is blank, and nothing between it and the table asks how long.
/// </summary>
public sealed class CreateRecurringExpenseRequestValidator
    : AbstractValidator<CreateRecurringExpenseCommand>
{
    public CreateRecurringExpenseRequestValidator()
    {
        // CallerUserId is injected from the JWT and never read from the body, so no rule guards it.
        // GroupId IS the body's to name — the manager checks membership before opening anything.
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.AnchorDate).NotEmpty();
        RuleFor(x => x.Description).MaximumLength(2000);
        RuleFor(x => x.FundingSource).IsInEnum();
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.AnchorDate)
            .When(x => x.EndDate.HasValue);
    }
}

public sealed class AmendRecurringExpenseRequestValidator
    : AbstractValidator<AmendRecurringExpenseCommand>
{
    public AmendRecurringExpenseRequestValidator()
    {
        // RecurringExpenseId comes from the route and CallerUserId from the JWT; neither is bound.
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Category).IsInEnum();
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
