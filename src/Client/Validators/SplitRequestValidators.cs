using Finance.Application.Commands;
using FluentValidation;

namespace Client.Validators;

public sealed class UpsertSplitRequestValidator : AbstractValidator<UpsertSplitCommand>
{
    public UpsertSplitRequestValidator()
    {
        // GroupId and UserId are injected from the route / JWT in the controller
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
