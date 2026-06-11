namespace Finance.Domain.ValueObjects;

public readonly record struct LedgerId(Guid Value)
{
    public static LedgerId New() => new(Guid.NewGuid());
    public static LedgerId Create(Guid value) => new(value);
}

public readonly record struct AccountId(Guid Value)
{
    public static AccountId New() => new(Guid.NewGuid());
    public static AccountId Create(Guid value) => new(value);
}

public readonly record struct JournalEntryId(Guid Value)
{
    public static JournalEntryId New() => new(Guid.NewGuid());
    public static JournalEntryId Create(Guid value) => new(value);
}

public readonly record struct PostingId(Guid Value)
{
    public static PostingId New() => new(Guid.NewGuid());
    public static PostingId Create(Guid value) => new(value);
}
