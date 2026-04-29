using Finance.Application.Contracts;

namespace Finance.Application.Managers;

public interface IBillWorkflowManager
{
    Task<BillResponse> CreateAsync(CreateBillRequest request, CancellationToken cancellationToken = default);
    Task<BillResponse?> UpdateAsync(UpdateBillRequest request, CancellationToken cancellationToken = default);
    Task<BillResponse?> DeactivateAsync(DeactivateBillRequest request, CancellationToken cancellationToken = default);
    Task<SplitResponse> UpsertSplitAsync(UpsertSplitRequest request, CancellationToken cancellationToken = default);
    Task<SplitResponse?> RemoveSplitAsync(RemoveSplitRequest request, CancellationToken cancellationToken = default);
}
