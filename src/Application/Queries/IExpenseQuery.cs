using Finance.Application.Dtos;
using Finance.Domain.ValueObjects;

namespace Finance.Application.Queries;

public sealed record ExpenseDetailParams(Guid ExpenseId);

public sealed record ListExpensesParams(
    Guid UserId,
    int Page = 1,
    int PageSize = 50,
    bool ActiveOnly = true);

public sealed record ListHouseholdExpensesParams(
    Guid HouseholdId,
    int Page = 1,
    int PageSize = 20,
    bool ActiveOnly = true,
    Guid? CallerId = null);

public sealed record HouseholdExpenseDetailParams(Guid ExpenseId);

public sealed record ListSplitsParams(Guid ExpenseId);

public interface IExpenseQuery
{
    // ── Personal expense queries ──────────────────────────────────────────────
    Task<ExpenseListDto> ListByUserAsync(ListExpensesParams request, CancellationToken cancellationToken = default);
    Task<ExpenseDto?> GetDetailAsync(ExpenseDetailParams request, CancellationToken cancellationToken = default);

    // ── Household expense queries ─────────────────────────────────────────────
    Task<HouseholdExpenseListDto> ListByHouseholdAsync(ListHouseholdExpensesParams request, CancellationToken cancellationToken = default);
    Task<HouseholdExpenseDto?> GetHouseholdDetailAsync(HouseholdExpenseDetailParams request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SplitDto>> ListSplitsAsync(ListSplitsParams request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a household expense with enriched splits (member name + paid status) and the caller's role.
    /// </summary>
    Task<HouseholdExpenseDetailDto?> GetHouseholdExpenseDetailAsync(Guid expenseId, Guid callerId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForUserAsync(UserId userId, string title, decimal amount, CancellationToken cancellationToken = default);

    // ── Expense-split queries (sub-entity of Expense) ────────────────────────
    /// Returns per-month, per-member contribution summaries for all household expenses.
    /// Recurring expenses are projected forward/back across the window.
    /// </summary>
    Task<IReadOnlyCollection<HouseholdMonthlyContributionsDto>> ListSplitsByHouseholdAsync(
        HouseholdId householdId, DateTime windowStart, DateTime windowEnd, CancellationToken cancellationToken = default);

}
