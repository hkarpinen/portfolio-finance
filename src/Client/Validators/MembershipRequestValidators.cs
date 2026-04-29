using Finance.Application.Contracts;
using FluentValidation;

namespace Client.Validators;

public sealed class InviteHouseholdMemberRequestValidator : AbstractValidator<InviteHouseholdMemberRequest>
{
    public InviteHouseholdMemberRequestValidator()
    {
        // HouseholdId is injected from the route by HouseholdsController.Invite
        // (request with { HouseholdId = id }) — never supplied in the request body.
        RuleFor(x => x.InvitedByUserId).NotEmpty();
        RuleFor(x => x.InvitationCode).NotEmpty().MaximumLength(64);
    }
}

public sealed class JoinHouseholdRequestValidator : AbstractValidator<JoinHouseholdRequest>
{
    public JoinHouseholdRequestValidator()
    {
        RuleFor(x => x.InvitationCode).NotEmpty().MaximumLength(64);
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class ChangeMembershipRoleRequestValidator : AbstractValidator<ChangeMembershipRoleRequest>
{
    public ChangeMembershipRoleRequestValidator()
    {
        RuleFor(x => x.MembershipId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class RemoveMembershipRequestValidator : AbstractValidator<RemoveMembershipRequest>
{
    public RemoveMembershipRequestValidator()
    {
        RuleFor(x => x.MembershipId).NotEmpty();
        RuleFor(x => x.RemovedByUserId).NotEmpty();
    }
}
