using Finance.Application.Commands;
using Finance.Application.Dtos;

namespace Finance.Application.Managers;

public interface IExpenseManager
{
    Task<ExpenseResponseDto> CreateAsync(CreateExpenseCommand request, CancellationToken cancellationToken = default);
    Task<ExpenseResponseDto?> UpdateAsync(UpdateExpenseCommand request, CancellationToken cancellationToken = default);
    Task<ExpenseResponseDto?> DeleteAsync(DeleteExpenseCommand request, CancellationToken cancellationToken = default);

    Task<ExpenseResponseDto> CreateGroupExpenseAsync(CreateGroupExpenseCommand request, CancellationToken cancellationToken = default);
    Task<ExpenseResponseDto?> UpdateGroupExpenseAsync(UpdateGroupExpenseCommand request, CancellationToken cancellationToken = default);
    Task<ExpenseResponseDto?> DeactivateGroupExpenseAsync(DeactivateExpenseCommand request, CancellationToken cancellationToken = default);

    Task<ShareDto> UpsertShareAsync(UpsertShareCommand request, CancellationToken cancellationToken = default);
    Task<ShareDto?> RemoveShareAsync(RemoveShareCommand request, CancellationToken cancellationToken = default);
    Task AllocateEvenlyAsync(Guid expenseId, IReadOnlyList<Guid> membershipIds, CancellationToken cancellationToken = default);

    // userId is authoritative: the role check already happened upstream, so this deliberately does
    // NOT override the actor with a caller. No-op if the expense is unknown.
    Task<ShareDto?> AssignShareAsync(Guid groupId, Guid expenseId, Guid userId, decimal amount, string currency, CancellationToken cancellationToken = default);

    // Null for personal bills and for self-payer no-ops; a group share resolves an outcome the
    // caller posts to the ledger.
    Task<SettlementOutcome?> MarkPaidAsync(MarkExpensePaidCommand request, CancellationToken cancellationToken = default);

    // Null for personal bills and no-ops; otherwise the outcome whose source the caller reverses.
    Task<SettlementOutcome?> MarkUnpaidAsync(MarkExpenseUnpaidCommand request, CancellationToken cancellationToken = default);

    // Pays the bill itself, choosing the funding account at payment time (which is why funding is a
    // command field and not expense state). Null if not a group expense or already paid.
    Task<VendorPaymentOutcome?> MarkVendorPaidAsync(MarkVendorPaidCommand request, CancellationToken cancellationToken = default);

    // Null if not a group expense or not currently paid.
    Task<VendorPaymentOutcome?> MarkVendorUnpaidAsync(MarkVendorUnpaidCommand request, CancellationToken cancellationToken = default);
}
