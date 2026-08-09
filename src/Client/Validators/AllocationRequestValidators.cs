using Finance.Application.Commands;
using FluentValidation;

namespace Client.Validators;

public sealed class UpsertAllocationRequestValidator : AbstractValidator<UpsertAllocationCommand>
{
    public UpsertAllocationRequestValidator()
    {
        // GroupId and UserId are injected from the route / JWT in the controller, never from the body.
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
