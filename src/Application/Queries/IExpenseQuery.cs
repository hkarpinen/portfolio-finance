using Finance.Application.Dtos;
using Finance.Application.Queries;
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
    // ── Personal expense queries ─────────────────────────────────────────────
    Task<ExpenseListDto> ListByUserAsync(ListExpensesParams request, CancellationToken cancellationToken = default);
    /// <summary>Returns all active personal expenses for a user without pagination.</summary>
    Task<IReadOnlyList<ExpenseDto>> GetAllActivePersonalByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<ExpenseDto?> GetDetailAsync(ExpenseDetailParams request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all (ExpenseId, OccurrenceDate) pairs that the user has paid within the date window,
    /// mapped to the timestamp the payment was recorded.
    /// </summary>
    Task<IReadOnlyDictionary<(Guid ExpenseId, DateTime OccurrenceDate), DateTime>> GetPaidOccurrencesAsync(
        UserId userId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    // ── Household expense queries (absorbed from ISharedExpenseQuery) ─────────
    Task<HouseholdExpenseListDto> ListByHouseholdAsync(ListHouseholdExpensesParams request, CancellationToken cancellationToken = default);
    Task<HouseholdExpenseDto?> GetHouseholdDetailAsync(HouseholdExpenseDetailParams request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SplitDto>> ListSplitsAsync(ListSplitsParams request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a household expense with enriched splits (member name + paid status) and the caller's role.
    /// </summary>
    Task<HouseholdExpenseDetailDto?> GetHouseholdExpenseDetailAsync(Guid expenseId, Guid callerId, CancellationToken cancellationToken = default);
}
