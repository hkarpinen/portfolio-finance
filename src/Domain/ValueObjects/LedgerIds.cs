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

public readonly record struct JournalLineId(Guid Value)
{
    public static JournalLineId New() => new(Guid.NewGuid());
    public static JournalLineId Create(Guid value) => new(value);
}
