namespace Finance.Domain.ValueObjects;

/// <summary>
/// Expense aggregate root identifier.
/// </summary>
public readonly record struct ExpenseId(Guid Value)
{
    public static ExpenseId New() => new(Guid.NewGuid());
    public static ExpenseId Create(Guid value) => new(value);
}
