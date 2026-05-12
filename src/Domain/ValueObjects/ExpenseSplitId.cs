namespace Finance.Domain.ValueObjects;

/// <summary>
/// ExpenseSplit aggregate root identifier.
/// </summary>
public readonly record struct ExpenseSplitId(Guid Value)
{
    public static ExpenseSplitId New() => new(Guid.NewGuid());
    public static ExpenseSplitId Create(Guid value) => new(value);
}
