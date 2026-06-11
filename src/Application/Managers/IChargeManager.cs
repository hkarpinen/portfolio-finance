using Finance.Application.Commands;
using Finance.Application.Dtos;

namespace Finance.Application.Managers;

public interface IChargeManager
{
    // ── Personal expense operations ──────────────────────────────────────────
    Task<ChargeResponseDto> CreateAsync(CreateChargeCommand request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> UpdateAsync(UpdateChargeCommand request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> DeleteAsync(DeleteChargeCommand request, CancellationToken cancellationToken = default);

    // ── Group/household expense operations ───────────────────────────────────
    Task<ChargeResponseDto> CreateGroupChargeAsync(CreateGroupChargeCommand request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> UpdateGroupChargeAsync(UpdateGroupChargeCommand request, CancellationToken cancellationToken = default);
    Task<ChargeResponseDto?> DeactivateGroupChargeAsync(DeactivateChargeCommand request, CancellationToken cancellationToken = default);

    // ── Allocation management ─────────────────────────────────────────────────────
    Task<AllocationDto> UpsertAllocationAsync(UpsertAllocationCommand request, CancellationToken cancellationToken = default);
    Task<AllocationDto?> RemoveAllocationAsync(RemoveAllocationCommand request, CancellationToken cancellationToken = default);
    Task AllocateEvenlyAsync(Guid expenseId, IReadOnlyList<Guid> membershipIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts a member's allocation (their share) on a group charge — create or update by
    /// (charge, user). The <paramref name="userId"/> is authoritative; this is the apply-side of a
    /// household-authorized <c>GroupAllocationAssigned</c> event (the role check already happened in
    /// household), so it does NOT override the actor with a caller. No-op if the charge is unknown.
    /// </summary>
    Task<AllocationDto?> AssignAllocationAsync(Guid groupId, Guid chargeId, Guid userId, decimal amount, string currency, CancellationToken cancellationToken = default);

    // ── Unified payment (routes internally based on charge type) ─────────────
    /// <summary>
    /// Marks the caller's share/bill paid. For a group allocation this resolves a
    /// <see cref="SettlementOutcome"/> so the Client can post it to the group ledger
    /// (the single source of truth); returns null for personal bills or self-payer no-ops.
    /// </summary>
    Task<SettlementOutcome?> MarkPaidAsync(MarkChargePaidCommand request, CancellationToken cancellationToken = default);

    /// <summary>Un-marks the caller's share paid. For a group allocation returns the
    /// <see cref="SettlementOutcome"/> whose source the Client reverses in the ledger;
    /// null for personal bills or no-ops.</summary>
    Task<SettlementOutcome?> MarkUnpaidAsync(MarkChargeUnpaidCommand request, CancellationToken cancellationToken = default);

    /// <summary>Mark the VENDOR paid for a group charge's occurrence (the bill itself), choosing the
    /// funding at payment time. Returns a <see cref="VendorPaymentOutcome"/> the Client posts to the
    /// ledger (Dr Vendor Payable / Cr funding); null if not a group charge or already paid.</summary>
    Task<VendorPaymentOutcome?> MarkVendorPaidAsync(MarkVendorPaidCommand request, CancellationToken cancellationToken = default);

    /// <summary>Undo a vendor payment; returns the <see cref="VendorPaymentOutcome"/> whose source
    /// the Client reverses in the ledger. Null if not a group charge or not currently paid.</summary>
    Task<VendorPaymentOutcome?> MarkVendorUnpaidAsync(MarkVendorUnpaidCommand request, CancellationToken cancellationToken = default);
}
