using Finance.Application.Commands;
using FluentValidation;

namespace Client.Validators;

public sealed class InviteHouseholdMemberRequestValidator : AbstractValidator<InviteHouseholdMemberCommand>
{
    public InviteHouseholdMemberRequestValidator()
    {
        // HouseholdId is injected from the route by HouseholdsController.Invite
        // (request with { HouseholdId = id }) — never supplied in the request body.
        RuleFor(x => x.InvitedByUserId).NotEmpty();
    }
}

public sealed class JoinHouseholdRequestValidator : AbstractValidator<JoinHouseholdCommand>
{
    public JoinHouseholdRequestValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
    }
}

public sealed class ChangeMembershipRoleRequestValidator : AbstractValidator<ChangeMembershipRoleCommand>
{
    public ChangeMembershipRoleRequestValidator()
    {
        RuleFor(x => x.MembershipId).NotEmpty();
        RuleFor(x => x.Role).IsInEnum();
    }
}

public sealed class RemoveMembershipRequestValidator : AbstractValidator<RemoveMembershipCommand>
{
    public RemoveMembershipRequestValidator()
    {
        RuleFor(x => x.MembershipId).NotEmpty();
        RuleFor(x => x.RemovedByUserId).NotEmpty();
    }
}
