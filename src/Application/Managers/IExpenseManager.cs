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
    Task SplitEvenlyAsync(Guid expenseId, IReadOnlyList<Guid> userIds, CancellationToken cancellationToken = default);

    // userId is authoritative: the role check already happened upstream, so this deliberately does
    // NOT override the actor with a caller. No-op if the expense is unknown.
    Task AssignShareAsync(Guid groupId, Guid expenseId, Guid userId, decimal amount, string currency, CancellationToken cancellationToken = default);

    /// <summary>
    /// One member paying another directly to square up. Commits the document and lets the outbox
    /// drive the posting, like every other mutation.
    /// </summary>
    Task SettleUpAsync(Guid groupId, Guid fromUserId, Guid toUserId, decimal amount, string currency, CancellationToken ct = default);

    Task MarkPaidAsync(MarkExpensePaidCommand request, CancellationToken cancellationToken = default);

    Task MarkUnpaidAsync(MarkExpenseUnpaidCommand request, CancellationToken cancellationToken = default);

    // Pays the expense itself, choosing the funding account at payment time — which is why funding
    // is a command field and not expense state.
    Task MarkVendorPaidAsync(MarkVendorPaidCommand request, CancellationToken cancellationToken = default);

    Task MarkVendorUnpaidAsync(MarkVendorUnpaidCommand request, CancellationToken cancellationToken = default);
}
