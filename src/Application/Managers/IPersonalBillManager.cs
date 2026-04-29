using Finance.Application.Contracts;

namespace Finance.Application.Managers;

public interface IPersonalBillManager
{
    Task<PersonalBillResponse> CreateAsync(CreatePersonalBillRequest request, CancellationToken cancellationToken = default);
    Task<PersonalBillResponse?> UpdateAsync(UpdatePersonalBillRequest request, CancellationToken cancellationToken = default);
    Task<PersonalBillResponse?> DeleteAsync(DeletePersonalBillRequest request, CancellationToken cancellationToken = default);
}
