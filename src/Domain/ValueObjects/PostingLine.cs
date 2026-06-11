namespace Finance.Domain.ValueObjects;

/// <summary>
/// An input line for composing a <c>JournalEntry</c>: which account, which side,
/// how much (always positive — the side carries the sign). Distinct from the
/// persisted <c>Posting</c> entity, which additionally has identity.
/// </summary>
public readonly record struct PostingLine(AccountId AccountId, EntryDirection Direction, Money Amount)
{
    public static PostingLine Debit(AccountId account, Money amount) => new(account, EntryDirection.Debit, amount);
    public static PostingLine Credit(AccountId account, Money amount) => new(account, EntryDirection.Credit, amount);
}
