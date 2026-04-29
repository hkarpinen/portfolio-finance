using Finance.Application.Contracts;
using Finance.Application.Managers.Dependencies;
using Finance.Domain.Aggregates;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Managers;

internal sealed class HouseholdWorkflowManager : IHouseholdWorkflowManager
{
    private readonly IHouseholdRepository _householdRepository;
    private readonly IHouseholdMembershipRepository _membershipRepository;

    public HouseholdWorkflowManager(
        IHouseholdRepository householdRepository,
        IHouseholdMembershipRepository membershipRepository)
    {
        _householdRepository = householdRepository;
        _membershipRepository = membershipRepository;
    }

    public async Task<HouseholdResponse> CreateAsync(CreateHouseholdRequest request, CancellationToken cancellationToken = default)
    {
        var household = Household.Create(
            request.Name,
            UserId.Create(request.OwnerId),
            request.CurrencyCode,
            request.Description);

        await _householdRepository.AddAsync(household, cancellationToken);

        var ownerMembership = HouseholdMembership.Create(household.Id, UserId.Create(request.OwnerId), HouseholdRole.Owner);
        await _membershipRepository.AddAsync(ownerMembership, cancellationToken);

        return Map(household);
    }

    public async Task<HouseholdResponse?> UpdateAsync(UpdateHouseholdRequest request, CancellationToken cancellationToken = default)
    {
        var household = await _householdRepository.GetByIdAsync(HouseholdId.Create(request.HouseholdId), cancellationToken);
        if (household is null)
        {
            return null;
        }

        household.Update(request.Name, request.Description);
        await _householdRepository.UpdateAsync(household, cancellationToken);
        return Map(household);
    }

    public async Task<HouseholdResponse?> TransferOwnershipAsync(TransferHouseholdOwnershipRequest request, CancellationToken cancellationToken = default)
    {
        var household = await _householdRepository.GetByIdAsync(HouseholdId.Create(request.HouseholdId), cancellationToken);
        if (household is null)
        {
            return null;
        }

        if (household.OwnerId.Value != request.RequestingUserId)
            throw new UnauthorizedAccessException("Only the current owner can transfer household ownership.");

        var memberships = await _membershipRepository.ListByHouseholdAsync(household.Id, cancellationToken);
        var newOwnerMembership = memberships.FirstOrDefault(m => m.UserId.Value == request.NewOwnerId && m.IsActive);
        if (newOwnerMembership is null)
            throw new InvalidOperationException("The new owner must be an active member of the household.");

        household.TransferOwnership(UserId.Create(request.NewOwnerId));
        await _householdRepository.UpdateAsync(household, cancellationToken);

        return Map(household);
    }

    public async Task<bool> DeleteAsync(DeleteHouseholdRequest request, CancellationToken cancellationToken = default)
    {
        var household = await _householdRepository.GetByIdAsync(HouseholdId.Create(request.HouseholdId), cancellationToken);
        if (household is null)
            return false;

        if (household.OwnerId.Value != request.RequestingUserId)
            throw new UnauthorizedAccessException("Only the owner can delete the household.");

        var memberships = await _membershipRepository.ListByHouseholdAsync(household.Id, cancellationToken);
        var activeMemberCount = memberships.Count(m => m.IsActive);
        if (activeMemberCount > 1)
            throw new InvalidOperationException("Cannot delete a household with other active members. Remove all other members first or transfer ownership.");

        household.Deactivate();
        await _householdRepository.UpdateAsync(household, cancellationToken);
        return true;
    }

    private static HouseholdResponse Map(Household household)
        => new(
            household.Id.Value,
            household.Name,
            household.Description,
            household.OwnerId.Value,
            household.CurrencyCode,
            household.IsActive,
            household.CreatedAt,
            household.UpdatedAt);
}
