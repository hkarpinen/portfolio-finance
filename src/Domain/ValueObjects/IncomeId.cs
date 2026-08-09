namespace Finance.Domain.ValueObjects;

public readonly record struct IncomeId(Guid Value)
{
    public static IncomeId New() => new(Guid.NewGuid());
    public static IncomeId Create(Guid value) => new(value);
}
