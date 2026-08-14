namespace Finance.Domain.ValueObjects;

// Amount is ALWAYS positive — the direction carries the sign. Distinct from the persisted JournalLine
// entity, which is the same data plus identity.
public readonly record struct JournalLineDraft(AccountId AccountId, EntryDirection Direction, Money Amount)
{
    public static JournalLineDraft Debit(AccountId account, Money amount) => new(account, EntryDirection.Debit, amount);
    public static JournalLineDraft Credit(AccountId account, Money amount) => new(account, EntryDirection.Credit, amount);
}
