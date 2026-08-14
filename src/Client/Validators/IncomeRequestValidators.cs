using Finance.Application.Commands;
using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;
using FluentValidation;

namespace Client.Validators;

public sealed class CreateIncomeRequestValidator : AbstractValidator<CreateIncomeCommand>
{
    public CreateIncomeRequestValidator()
    {
        // UserId is injected from the JWT in the controller and is never read from the request body,
        // which is why no rule guards it.
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Source).NotEmpty().MaximumLength(200);
        RuleFor(x => x.QuotedAs).IsInEnum();
        RuleFor(x => x.PaidEvery).IsInEnum().When(x => x.PaidEvery.HasValue);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate.HasValue);
    }
}

/// <summary>
/// The shape of one deduction. What it MEANS — that a percentage cannot exceed 100, that the four
/// tax types are engine-computed and cannot be stored by hand — stays in
/// <see cref="Finance.Domain.ValueObjects.PayrollDeduction"/>, which answers with an
/// ArgumentException the pipeline already turns into a 400. This bounds the label, which nothing
/// between the request and the 200-char column was checking, and rejects an enum value out of range,
/// which JSON binding otherwise accepts as a number nobody named.
/// </summary>
public sealed class PayrollDeductionValidator : AbstractValidator<PayrollDeductionDto>
{
    public PayrollDeductionValidator()
    {
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.Value).GreaterThan(0);
    }
}

public sealed class AddDeductionRequestValidator : AbstractValidator<AddDeductionCommand>
{
    public AddDeductionRequestValidator()
    {
        RuleFor(x => x.IncomeId).NotEmpty();
        RuleFor(x => x.Deduction).NotNull().SetValidator(new PayrollDeductionValidator());
    }
}

public sealed class RemoveDeductionRequestValidator : AbstractValidator<RemoveDeductionCommand>
{
    public RemoveDeductionRequestValidator()
    {
        RuleFor(x => x.IncomeId).NotEmpty();
        // Parsed to DeductionType downstream, so an unknown name is rejected here by the name it
        // arrived under rather than by the parser's message.
        RuleFor(x => x.DeductionType).NotEmpty().IsEnumName(typeof(DeductionType), caseSensitive: false);
        RuleFor(x => x.Label).NotEmpty().MaximumLength(200);
    }
}

public sealed class SetTaxProfileRequestValidator : AbstractValidator<SetTaxProfileCommand>
{
    public SetTaxProfileRequestValidator()
    {
        RuleFor(x => x.IncomeId).NotEmpty();

        // Null is the documented way to CLEAR the profile, so it is only checked when one is sent.
        When(x => x.TaxProfile is not null, () =>
        {
            RuleFor(x => x.TaxProfile!.FilingStatus).IsInEnum();
            RuleFor(x => x.TaxProfile!.StateCode).NotEmpty().Length(2);
            RuleFor(x => x.TaxProfile!.FederalAllowances).GreaterThanOrEqualTo(0);
            RuleFor(x => x.TaxProfile!.StateAllowances).GreaterThanOrEqualTo(0);
        });
    }
}

public sealed class UpdateIncomeRequestValidator : AbstractValidator<UpdateIncomeCommand>
{
    public UpdateIncomeRequestValidator()
    {
        RuleFor(x => x.IncomeId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Source).NotEmpty().MaximumLength(200);
        RuleFor(x => x.QuotedAs).IsInEnum();
        RuleFor(x => x.PaidEvery).IsInEnum().When(x => x.PaidEvery.HasValue);
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate)
            .When(x => x.EndDate.HasValue);
    }
}
