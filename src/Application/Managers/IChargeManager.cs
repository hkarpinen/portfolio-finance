using Finance.Application.Commands;
using Finance.Application.Dtos;

namespace Finance.Application.Managers;

public interface IChargeManager
{
    Task<ChargeResponseDto> CreateAsync(CreateChargeCommand request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> UpdateAsync(UpdateChargeCommand request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> DeleteAsync(DeleteChargeCommand request, CancellationToken cancellationToken = default);

    Task<ChargeResponseDto> CreateGroupChargeAsync(CreateGroupChargeCommand request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> UpdateGroupChargeAsync(UpdateGroupChargeCommand request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> DeactivateGroupChargeAsync(DeactivateChargeCommand request, CancellationToken cancellationToken = default);

    Task<AllocationDto> UpsertAllocationAsync(UpsertAllocationCommand request, CancellationToken cancellationToken = default);
    Task<AllocationDto?> RemoveAllocationAsync(RemoveAllocationCommand request, CancellationToken cancellationToken = default);
    Task AllocateEvenlyAsync(Guid expenseId, IReadOnlyList<Guid> membershipIds, CancellationToken cancellationToken = default);

    // userId is authoritative: the role check already happened upstream, so this deliberately does
    // NOT override the actor with a caller. No-op if the charge is unknown.
    Task<AllocationDto?> AssignAllocationAsync(Guid groupId, Guid chargeId, Guid userId, decimal amount, string currency, CancellationToken cancellationToken = default);

    // Null for personal bills and for self-payer no-ops; a group allocation resolves an outcome the
    // caller posts to the ledger.
    Task<SettlementOutcome?> MarkPaidAsync(MarkChargePaidCommand request, CancellationToken cancellationToken = default);

    // Null for personal bills and no-ops; otherwise the outcome whose source the caller reverses.
    Task<SettlementOutcome?> MarkUnpaidAsync(MarkChargeUnpaidCommand request, CancellationToken cancellationToken = default);

    // Pays the bill itself, choosing the funding account at payment time (which is why funding is a
    // command field and not charge state). Null if not a group charge or already paid.
    Task<VendorPaymentOutcome?> MarkVendorPaidAsync(MarkVendorPaidCommand request, CancellationToken cancellationToken = default);

    // Null if not a group charge or not currently paid.
    Task<VendorPaymentOutcome?> MarkVendorUnpaidAsync(MarkVendorUnpaidCommand request, CancellationToken cancellationToken = default);
}
