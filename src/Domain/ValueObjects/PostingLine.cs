namespace Finance.Domain.ValueObjects;

// Amount is ALWAYS positive — the direction carries the sign. Distinct from the persisted Posting
// entity, which is the same data plus identity.
public readonly record struct PostingLine(AccountId AccountId, EntryDirection Direction, Money Amount)
{
    public static PostingLine Debit(AccountId account, Money amount) => new(account, EntryDirection.Debit, amount);
    public static PostingLine Credit(AccountId account, Money amount) => new(account, EntryDirection.Credit, amount);
}
