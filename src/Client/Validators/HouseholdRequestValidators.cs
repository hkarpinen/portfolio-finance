using Bills.Application.Contracts;
using FluentValidation;

namespace Client.Validators;

public sealed class CreateHouseholdRequestValidator : AbstractValidator<CreateHouseholdRequest>
{
    public CreateHouseholdRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        // OwnerId is injected from JWT by HouseholdsController.Create
        // (request with { OwnerId = userId.Value }) — never supplied in the request body.
        RuleFor(x => x.CurrencyCode).NotEmpty().Length(3);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
    }
}

public sealed class UpdateHouseholdRequestValidator : AbstractValidator<UpdateHouseholdRequest>
{
    public UpdateHouseholdRequestValidator()
    {
        // HouseholdId is injected from the route by HouseholdsController.Update
        // (request with { HouseholdId = id }) — never supplied in the request body.
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description is not null);
    }
}

public sealed class TransferHouseholdOwnershipRequestValidator : AbstractValidator<TransferHouseholdOwnershipRequest>
{
    public TransferHouseholdOwnershipRequestValidator()
    {
        RuleFor(x => x.HouseholdId).NotEmpty();
        RuleFor(x => x.NewOwnerId).NotEmpty();
    }
}
