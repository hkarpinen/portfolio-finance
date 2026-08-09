namespace Finance.Application.Queries;

public interface IGroupQuery
{
    // A departed member's projection row survives (their accounts and debts outlive departure)
    // but goes inactive, and inactive must not grant access.
    Task<bool> IsCurrentMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    // Membership alone is not enough: charge ids are looked up globally, so a member of their own
    // group could otherwise name another group's charge under their own group's URL.
    Task<bool> ChargeBelongsToGroupAsync(Guid groupId, Guid chargeId, CancellationToken cancellationToken = default);
}
