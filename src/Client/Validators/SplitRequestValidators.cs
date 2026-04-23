using Bills.Application.Contracts;
using FluentValidation;

namespace Client.Validators;

public sealed class UpsertSplitRequestValidator : AbstractValidator<UpsertSplitRequest>
{
    public UpsertSplitRequestValidator()
    {
        // BillId, HouseholdId, and UserId are injected from the route / JWT in the controller
        // (request with { BillId = billId, HouseholdId = householdId, UserId = userId.Value })
        // so they are always Guid.Empty when FluentValidation sees the body — do not validate them here.
        RuleFor(x => x.MembershipId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}
