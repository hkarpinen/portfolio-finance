using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

// NormalBalance is derived from AccountType, never stored, so increases/decreases always map to
// debit/credit correctly. ParentAccountId makes accounts hierarchical for rollups: a parent's
// balance is its own journal lines plus its children's. Balances are never stored on the account —
// always derived from lines, so an account cannot drift out of sync with the journal.
public sealed class Account : IAggregateRoot
{
    private readonly List<DomainEvent> _domainEvents = new();

    public AccountId Id { get; private set; }
    public LedgerId LedgerId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public AccountType AccountType { get; private set; }
    public AccountId? ParentAccountId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public NormalBalance NormalBalance => AccountType.NormalBalance();

    /// <summary>
    /// What these lines leave on this account, oriented to its normal balance: a debit-normal
    /// account (asset, expense) reads positive when net-debited, a credit-normal one (liability,
    /// equity, revenue) when net-credited.
    ///
    /// Takes the lines rather than fetching them — an account does not hold its own postings, and
    /// a stored balance could drift from the journal it came from. But the ORIENTATION is the
    /// account's own, so nobody has to pass it and nobody can pass the wrong one.
    /// </summary>
    public decimal BalanceFrom(IEnumerable<JournalLine> lines)
    {
        decimal debits = 0m, credits = 0m;
        foreach (var l in lines)
        {
            if (l.Direction == EntryDirection.Debit) debits += l.Amount.Amount;
            else credits += l.Amount.Amount;
        }
        return NormalBalance == NormalBalance.Debit ? debits - credits : credits - debits;
    }

    public IReadOnlyList<DomainEvent> GetDomainEvents() => _domainEvents.AsReadOnly();
    public void ClearDomainEvents() => _domainEvents.Clear();

    private Account() { }

    public static Account Open(
        LedgerId ledgerId,
        string code,
        string name,
        AccountType accountType,
        AccountId? parentAccountId = null)
    {
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Account code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Account name is required.", nameof(name));

        var account = new Account
        {
            Id = AccountId.New(),
            LedgerId = ledgerId,
            Code = code,
            Name = name,
            AccountType = accountType,
            ParentAccountId = parentAccountId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
        account._domainEvents.Add(new AccountOpened(account.Id, ledgerId, code, accountType));
        return account;
    }
}
