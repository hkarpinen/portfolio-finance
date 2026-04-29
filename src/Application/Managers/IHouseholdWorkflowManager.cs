using Finance.Application.Contracts;

namespace Finance.Application.Managers;

public interface IHouseholdWorkflowManager
{
    Task<HouseholdResponse> CreateAsync(CreateHouseholdRequest request, CancellationToken cancellationToken = default);
    Task<HouseholdResponse?> UpdateAsync(UpdateHouseholdRequest request, CancellationToken cancellationToken = default);
    Task<HouseholdResponse?> TransferOwnershipAsync(TransferHouseholdOwnershipRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(DeleteHouseholdRequest request, CancellationToken cancellationToken = default);
}
