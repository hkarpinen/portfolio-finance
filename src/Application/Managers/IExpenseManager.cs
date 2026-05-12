using Finance.Application.Commands;
using Finance.Application.Dtos;

namespace Finance.Application.Managers;

public interface IExpenseManager
{
    // ── Personal expense operations ──────────────────────────────────────────
    Task<ExpenseDto> CreateAsync(CreateExpenseCommand request, CancellationToken cancellationToken = default);
    Task<ExpenseDto?> UpdateAsync(UpdateExpenseCommand request, CancellationToken cancellationToken = default);
    Task<ExpenseDto?> DeleteAsync(DeleteExpenseCommand request, CancellationToken cancellationToken = default);

    // ── Household expense operations ─────────────────────────────────────────
    Task<HouseholdExpenseDto> CreateHouseholdExpenseAsync(CreateHouseholdExpenseCommand request, CancellationToken cancellationToken = default);
    Task<HouseholdExpenseDto?> UpdateHouseholdExpenseAsync(UpdateHouseholdExpenseCommand request, CancellationToken cancellationToken = default);
    Task<HouseholdExpenseDto?> DeactivateHouseholdExpenseAsync(DeactivateExpenseCommand request, CancellationToken cancellationToken = default);

    // ── Split management ─────────────────────────────────────────────────────
    Task<SplitDto> UpsertSplitAsync(UpsertSplitCommand request, CancellationToken cancellationToken = default);
    Task<SplitDto?> RemoveSplitAsync(RemoveSplitCommand request, CancellationToken cancellationToken = default);
    Task SplitEvenlyAsync(Guid expenseId, IReadOnlyList<Guid> membershipIds, CancellationToken cancellationToken = default);

    // ── Unified payment (routes internally based on expense type) ────────────
    Task MarkPaidAsync(MarkExpensePaidCommand request, CancellationToken cancellationToken = default);
    Task MarkUnpaidAsync(MarkExpenseUnpaidCommand request, CancellationToken cancellationToken = default);
}
