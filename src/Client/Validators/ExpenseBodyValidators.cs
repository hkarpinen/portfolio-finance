using Finance.Application.Commands;
using FluentValidation;

namespace Client.Validators;

/// <summary>
/// The three small bodies on the expense routes. Each carries one or two fields, which is exactly
/// why none of them had a validator — and why an empty user list or a zero-date reached a manager
/// that had no reason to expect one.
/// </summary>
public sealed class PaymentOccurrenceBodyValidator : AbstractValidator<PaymentOccurrenceBody>
{
    public PaymentOccurrenceBodyValidator()
    {
        // A missing date binds to default(DateTime), which is a real date the schedule never named.
        RuleFor(x => x.OccurrenceDate).NotEmpty();
    }
}

public sealed class SettleUpTransferBodyValidator : AbstractValidator<SettleUpTransferBody>
{
    public SettleUpTransferBodyValidator()
    {
        // Who it goes TO is the body's to name; who it comes FROM is the caller.
        // That they are not the same person is MemberTransfer's rule, not this one's.
        RuleFor(x => x.ToUserId).NotEmpty();
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
    }
}

public sealed class AllocateEvenlyBodyValidator : AbstractValidator<AllocateEvenlyBody>
{
    public AllocateEvenlyBodyValidator()
    {
        // An empty list divides a cost among nobody; a repeated id gives one member two shares.
        RuleFor(x => x.UserIds).NotEmpty();
        RuleForEach(x => x.UserIds).NotEmpty();
        RuleFor(x => x.UserIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .When(x => x.UserIds is not null)
            .WithMessage("The same member cannot be named twice.");
    }
}
