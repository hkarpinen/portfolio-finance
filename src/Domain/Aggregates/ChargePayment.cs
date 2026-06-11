using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Records that a user has paid a specific occurrence of their expense.
/// Keyed on (ChargeId, OccurrenceDate) — each unique occurrence can have at most one payment record.
/// </summary>
public class ChargePayment : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public ChargePaymentId Id { get; private set; }
    public ChargeId ChargeId { get; private set; }
    public UserId UserId { get; private set; }
    public DateTime OccurrenceDate { get; private set; }
    public DateTime PaidAt { get; private set; }
    public string? TransactionReference { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private ChargePayment() { }

    public static ChargePayment Create(ChargeId chargeId, UserId userId, DateTime occurrenceDate, string? transactionReference = null)
    {
        var payment = new ChargePayment
        {
            Id = ChargePaymentId.New(),
            ChargeId = chargeId,
            UserId = userId,
            OccurrenceDate = DateTime.SpecifyKind(occurrenceDate.Date, DateTimeKind.Utc),
            PaidAt = DateTime.UtcNow,
            TransactionReference = transactionReference,
        };

        payment._domainEvents.Add(new ChargePaid(chargeId, userId, payment.OccurrenceDate, payment.PaidAt));
        return payment;
    }

    public void Remove()
    {
        _domainEvents.Add(new ChargeUnpaid(ChargeId, UserId, OccurrenceDate));
    }
}
