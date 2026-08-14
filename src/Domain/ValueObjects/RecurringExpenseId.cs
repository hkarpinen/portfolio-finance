namespace Finance.Domain.ValueObjects;

public readonly record struct RecurringExpenseId(Guid Value)
{
    public static RecurringExpenseId New() => new(Guid.NewGuid());
    public static RecurringExpenseId Create(Guid value) => new(value);
}
