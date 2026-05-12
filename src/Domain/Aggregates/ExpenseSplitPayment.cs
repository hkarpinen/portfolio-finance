using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Records that a user has paid their split for a specific occurrence of a household expense.
/// Keyed on (ExpenseSplitId, OccurrenceDate) — each member × occurrence can have at most one payment record.
/// </summary>
public class ExpenseSplitPayment : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public ExpenseSplitPaymentId Id { get; private set; }
    public ExpenseSplitId ExpenseSplitId { get; private set; }
    public ExpenseId ExpenseId { get; private set; }
    public HouseholdId HouseholdId { get; private set; }
    public UserId UserId { get; private set; }
    public DateTime OccurrenceDate { get; private set; }
    public DateTime PaidAt { get; private set; }
    public string? TransactionReference { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private ExpenseSplitPayment() { }

    public static ExpenseSplitPayment Create(
        ExpenseSplitId expenseSplitId,
        ExpenseId expenseId,
        HouseholdId householdId,
        UserId userId,
        DateTime occurrenceDate,
        string? transactionReference = null)
    {
        var payment = new ExpenseSplitPayment
        {
            Id = ExpenseSplitPaymentId.New(),
            ExpenseSplitId = expenseSplitId,
            ExpenseId = expenseId,
            HouseholdId = householdId,
            UserId = userId,
            OccurrenceDate = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc),
            PaidAt = DateTime.UtcNow,
            TransactionReference = transactionReference,
        };

        payment._domainEvents.Add(new ExpenseSplitPaid(expenseSplitId, expenseId, householdId, userId, payment.OccurrenceDate));
        return payment;
    }
}
