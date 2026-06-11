using Finance.Domain.Events;
using Finance.Domain.ValueObjects;

namespace Finance.Domain.Aggregates;

/// <summary>
/// A single line in a ledger's chart of accounts — the atomic unit every posting
/// hits. Typed (P3): <see cref="NormalBalance"/> is derived from <see cref="AccountType"/>,
/// so increases/decreases map to debit/credit correctly. Hierarchical via
/// <see cref="ParentAccountId"/> for rollups (a parent's balance = its own postings +
/// its children). Balances are NEVER stored here — they are derived from postings,
/// which keeps the account from drifting out of sync with the journal.
/// </summary>
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

    /// <summary>The side this account's balance normally sits on (derived, never stored redundantly).</summary>
    public NormalBalance NormalBalance => AccountType.NormalBalance();

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
