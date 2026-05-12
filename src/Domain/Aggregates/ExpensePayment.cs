using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Records that a user has paid a specific occurrence of their expense.
/// Keyed on (ExpenseId, OccurrenceDate) — each unique occurrence can have at most one payment record.
/// </summary>
public class ExpensePayment : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public ExpensePaymentId Id { get; private set; }
    public ExpenseId ExpenseId { get; private set; }
    public UserId UserId { get; private set; }
    public DateTime OccurrenceDate { get; private set; }
    public DateTime PaidAt { get; private set; }
    public string? TransactionReference { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private ExpensePayment() { }

    public static ExpensePayment Create(ExpenseId expenseId, UserId userId, DateTime occurrenceDate, string? transactionReference = null)
    {
        var payment = new ExpensePayment
        {
            Id = ExpensePaymentId.New(),
            ExpenseId = expenseId,
            UserId = userId,
            OccurrenceDate = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc),
            PaidAt = DateTime.UtcNow,
            TransactionReference = transactionReference,
        };

        payment._domainEvents.Add(new ExpensePaid(expenseId, userId, payment.OccurrenceDate, payment.PaidAt));
        return payment;
    }
}
