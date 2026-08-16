using Finance.Domain.Engines;
using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// Money arriving. A salary, a refund, somebody paying you back through your bank — anything that
/// landed in an account from outside the books.
///
/// What <see cref="Expense"/> is to a cost. There was no document for this at all, which is why
/// every deposit was invisible: the ledger only ever learned about money leaving, so a balance
/// drifted from reality by every payment in.
///
/// It carries no deductions. Gross-and-withholding is a payslip's business, and what a bank sees
/// is the net that actually arrived — inventing the gross from it would be a guess written down as
/// a fact.
/// </summary>
public sealed class Receipt : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public ReceiptId Id { get; private set; }

    /// <summary>Whose books it lands in. A person's own, always — money arriving in somebody's
    /// bank account is theirs before it is anybody else's.</summary>
    public AccountingEntity Owner { get; private set; }

    public Guid IntoAccountId { get; private set; }

    /// <summary>Where it came from, as the outside world named it — an employer, a shop refunding
    /// you. Becomes the income account it is credited to.</summary>
    public string Source { get; private set; } = string.Empty;

    public Money Amount { get; private set; }

    /// <summary>The day it arrived, which is the period it belongs to.</summary>
    public DateTime ReceivedOn { get; private set; }

    public bool IsVoid { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Receipt() { }

    public static Receipt Record(
        UserId owner, Guid intoAccountId, string source, Money amount, DateTime receivedOn)
    {
        if (string.IsNullOrWhiteSpace(source))
            throw new ArgumentException("Money arriving came from somewhere.", nameof(source));
        if (amount.Amount <= 0m)
            throw new ArgumentException(
                "A receipt is money arriving; nothing or less than nothing did not arrive.", nameof(amount));

        var receipt = new Receipt
        {
            Id = ReceiptId.New(),
            Owner = AccountingEntity.Person(owner),
            IntoAccountId = intoAccountId,
            Source = source,
            Amount = amount,
            ReceivedOn = DateTime.SpecifyKind(receivedOn.Date, DateTimeKind.Utc),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        receipt._domainEvents.Add(new ReceiptRecorded(
            receipt.Id, owner, intoAccountId, source, amount, receipt.ReceivedOn));
        return receipt;
    }

    /// <summary>
    /// It did not arrive after all — a pending deposit that never settled, or a provider
    /// correcting itself. Voided rather than deleted, because the books have to unwind what they
    /// were told and an erased row cannot be unwound.
    /// </summary>
    public void Void()
    {
        if (IsVoid) return;

        IsVoid = true;
        UpdatedAt = DateTime.UtcNow;
        _domainEvents.Add(new ReceiptVoided(Id, Owner.Id));
    }

    public string LedgerSource => LedgerSources.Receipt(Id.Value);
}
