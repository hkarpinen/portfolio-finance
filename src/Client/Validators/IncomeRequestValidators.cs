using Finance.Application.Commands;
using FluentValidation;

namespace Client.Validators;

public sealed class CreateIncomeRequestValidator : AbstractValidator<CreateIncomeCommand>
{
    public CreateIncomeRequestValidator()
    {
        // UserId is injected from JWT by both IncomeController and UserIncomeController
        // (request with { UserId = userId.Value }) — never supplied in the request body.
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
